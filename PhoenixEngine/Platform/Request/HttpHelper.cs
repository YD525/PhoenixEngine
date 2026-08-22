using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixEngine.Request
{
    /// <summary>
    /// Sends bounded HTTP requests through reusable clients with normal platform TLS validation.
    /// </summary>
    public class HttpHelper
    {
        private const int AbsoluteMaximumBodyBytes = 64 * 1024 * 1024;
        private const int AbsoluteMaximumHeaderBytes = 256 * 1024;
        private const int AbsoluteMaximumRetries = 5;
        private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(30);

        private readonly ConcurrentDictionary<HttpClientConfiguration, Lazy<HttpClient>> _clients;
        private readonly HttpClient _clientOverride;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

        /// <summary>
        /// Creates a transport that reuses clients for each distinct proxy, certificate, and redirect configuration.
        /// </summary>
        public HttpHelper()
            : this(null, Task.Delay)
        {
        }

        /// <summary>
        /// Creates a transport around deterministic test collaborators without changing production handler policy.
        /// </summary>
        /// <param name="client">The caller-owned client used for every request.</param>
        /// <param name="delayAsync">The cancellable delay used between retries.</param>
        /// <remarks>The caller retains ownership of <paramref name="client"/>.</remarks>
        internal HttpHelper(HttpClient client, Func<TimeSpan, CancellationToken, Task> delayAsync)
        {
            _clientOverride = client;
            _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
            _clients = new ConcurrentDictionary<HttpClientConfiguration, Lazy<HttpClient>>();
        }

        /// <summary>
        /// Sends a request synchronously for compatibility with existing translation-node contracts.
        /// </summary>
        /// <param name="item">The request settings to validate and apply.</param>
        /// <returns>The bounded response or a structured, user-safe failure.</returns>
        /// <remarks>
        /// UI and newly asynchronous code should use <see cref="GetHtmlAsync(HttpItem, CancellationToken)"/>.
        /// </remarks>
        public HttpResult GetHtml(HttpItem item)
        {
            return GetHtmlAsync(item, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Sends a bounded request asynchronously and observes cancellation throughout the operation.
        /// </summary>
        /// <param name="item">The request settings to validate and apply.</param>
        /// <param name="cancellationToken">A token that cancels request, response, and retry-delay work.</param>
        /// <returns>A task containing the response or a structured, user-safe failure.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Each request is disposed by the using statement after configuration validation.")]
        public async Task<HttpResult> GetHtmlAsync(HttpItem item, CancellationToken cancellationToken)
        {
            HttpRequestSettings settings;
            HttpClient client;
            try
            {
                settings = HttpRequestSettings.Create(item);
                client = _clientOverride ?? GetClient(settings.ClientConfiguration);
            }
            catch (Exception exception) when (IsConfigurationException(exception))
            {
                return HttpResult.Failure(
                    HttpFailureKind.Configuration,
                    "Request configuration failed.",
                    0);
            }

            using (CancellationTokenSource timeoutSource = new CancellationTokenSource(settings.Timeout))
            using (CancellationTokenSource operationSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token))
            {
                CancellationToken operationToken = operationSource.Token;
                int maximumAttempts = settings.MaximumRetries + 1;

                for (int attempt = 1; attempt <= maximumAttempts; attempt++)
                {
                    HttpRequestMessage request;
                    try
                    {
                        request = CreateRequest(settings);
                    }
                    catch (Exception exception) when (IsConfigurationException(exception))
                    {
                        return HttpResult.Failure(
                            HttpFailureKind.Configuration,
                            "Request configuration failed.",
                            attempt);
                    }

                    using (request)
                    try
                    {
                        using (HttpResponseMessage response = await client.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            operationToken).ConfigureAwait(false))
                        {
                            if (attempt < maximumAttempts && ShouldRetry(settings, response.StatusCode))
                            {
                                await _delayAsync(
                                    GetRetryDelay(response, attempt),
                                    operationToken).ConfigureAwait(false);
                                continue;
                            }

                            return await ReadResponseAsync(
                                settings,
                                response,
                                attempt,
                                operationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return cancellationToken.IsCancellationRequested
                            ? HttpResult.Failure(HttpFailureKind.Cancelled, "Request cancelled.", attempt)
                            : HttpResult.Failure(HttpFailureKind.Timeout, "Request timed out.", attempt);
                    }
                    catch (HttpRequestException exception)
                    {
                        if (IsTlsValidationFailure(exception))
                        {
                            return HttpResult.Failure(
                                HttpFailureKind.TlsValidation,
                                "TLS validation failed.",
                                attempt);
                        }
                        if (attempt < maximumAttempts && settings.CanRetry)
                        {
                            HttpResult cancellationResult = await DelayForRetryAsync(
                                GetRetryDelay(null, attempt),
                                attempt,
                                cancellationToken,
                                operationToken).ConfigureAwait(false);
                            if (cancellationResult != null)
                                return cancellationResult;
                            continue;
                        }

                        return HttpResult.Failure(HttpFailureKind.Network, "Request failed.", attempt);
                    }
                    catch (ResponseSizeLimitException)
                    {
                        return HttpResult.Failure(
                            HttpFailureKind.ResponseTooLarge,
                            "Response exceeds the configured size limit.",
                            attempt);
                    }
                    catch (ResponseHeaderLimitException)
                    {
                        return HttpResult.Failure(
                            HttpFailureKind.HeadersTooLarge,
                            "Response headers exceed the configured size limit.",
                            attempt);
                    }
                    catch (InvalidDataException)
                    {
                        return HttpResult.Failure(
                            HttpFailureKind.MalformedResponse,
                            "Response data is invalid.",
                            attempt);
                    }
                    catch (IOException)
                    {
                        if (attempt < maximumAttempts && settings.CanRetry)
                        {
                            HttpResult cancellationResult = await DelayForRetryAsync(
                                GetRetryDelay(null, attempt),
                                attempt,
                                cancellationToken,
                                operationToken).ConfigureAwait(false);
                            if (cancellationResult != null)
                                return cancellationResult;
                            continue;
                        }

                        return HttpResult.Failure(HttpFailureKind.Network, "Request failed.", attempt);
                    }
                }
            }

            return HttpResult.Failure(HttpFailureKind.Network, "Request failed.", 0);
        }

        /// <summary>Creates a production-equivalent handler for TLS policy verification.</summary>
        /// <param name="item">The request settings that define handler-specific configuration.</param>
        /// <returns>A caller-owned handler that uses normal platform server-certificate validation.</returns>
        internal static HttpClientHandler CreateHandlerForTesting(HttpItem item)
        {
            return CreateHandler(HttpRequestSettings.Create(item).ClientConfiguration);
        }

        private HttpClient GetClient(HttpClientConfiguration configuration)
        {
            Lazy<HttpClient> lazyClient = _clients.GetOrAdd(
                configuration,
                key => new Lazy<HttpClient>(
                    () => CreateClient(key),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            return lazyClient.Value;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "HttpClient takes ownership of the newly created handler through disposeHandler=true.")]
        private static HttpClient CreateClient(HttpClientConfiguration configuration)
        {
            return new HttpClient(CreateHandler(configuration), true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        private static HttpClientHandler CreateHandler(HttpClientConfiguration configuration)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                AllowAutoRedirect = configuration.AllowAutoRedirect,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CheckCertificateRevocationList = true,
                ClientCertificateOptions = ClientCertificateOption.Manual,
                Credentials = configuration.Credentials,
                MaxConnectionsPerServer = configuration.ConnectionLimit,
                MaxResponseHeadersLength = AbsoluteMaximumHeaderBytes / 1024,
                Proxy = configuration.Proxy,
                UseCookies = false,
                UseProxy = configuration.Proxy != null
            };

            if (configuration.MaximumAutomaticRedirections > 0)
                handler.MaxAutomaticRedirections = configuration.MaximumAutomaticRedirections;
            foreach (X509Certificate certificate in configuration.ClientCertificates)
                handler.ClientCertificates.Add(certificate);
            if (!string.IsNullOrEmpty(configuration.ClientCertificatePath))
                handler.ClientCertificates.Add(new X509Certificate2(configuration.ClientCertificatePath));

            return handler;
        }

        private static HttpRequestMessage CreateRequest(HttpRequestSettings settings)
        {
            HttpRequestMessage request = new HttpRequestMessage(settings.Method, settings.Uri)
            {
                Version = settings.ProtocolVersion
            };

            try
            {
                request.Content = CreateContent(settings);
                CopyHeaders(settings, request);
                return request;
            }
            catch
            {
                request.Dispose();
                throw;
            }
        }

        private static HttpContent CreateContent(HttpRequestSettings settings)
        {
            if (!settings.HasRequestBody)
                return null;

            HttpContent content;
            switch (settings.PostDataType)
            {
                case PostDataType.Byte:
                    content = new ByteArrayContent(settings.PostDataBytes ?? Array.Empty<byte>());
                    break;
                case PostDataType.FilePath:
                    FileStream stream = new FileStream(
                        settings.PostData,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);
                    content = new StreamContent(stream);
                    break;
                default:
                    content = new ByteArrayContent(
                        settings.PostEncoding.GetBytes(settings.PostData ?? string.Empty));
                    break;
            }

            if (!string.IsNullOrWhiteSpace(settings.ContentType))
            {
                MediaTypeHeaderValue contentType;
                if (!MediaTypeHeaderValue.TryParse(settings.ContentType, out contentType))
                {
                    content.Dispose();
                    throw new FormatException("The request content type is invalid.");
                }

                content.Headers.ContentType = contentType;
            }

            return content;
        }

        private static void CopyHeaders(HttpRequestSettings settings, HttpRequestMessage request)
        {
            foreach (KeyValuePair<string, string> header in settings.Headers)
            {
                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) &&
                    request.Content != null)
                {
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.Accept))
                request.Headers.TryAddWithoutValidation("Accept", settings.Accept);
            if (!string.IsNullOrWhiteSpace(settings.UserAgent))
                request.Headers.TryAddWithoutValidation("User-Agent", settings.UserAgent);
            if (!string.IsNullOrWhiteSpace(settings.Cookie))
                request.Headers.TryAddWithoutValidation("Cookie", settings.Cookie);
            if (!string.IsNullOrWhiteSpace(settings.Referer))
                request.Headers.Referrer = new Uri(settings.Referer, UriKind.Absolute);
            if (!string.IsNullOrWhiteSpace(settings.Host))
                request.Headers.Host = settings.Host;
            if (settings.IfModifiedSince.HasValue)
                request.Headers.IfModifiedSince = settings.IfModifiedSince.Value;

            request.Headers.ExpectContinue = settings.Expect100Continue;
            request.Headers.ConnectionClose = !settings.KeepAlive;
        }

        private static async Task<HttpResult> ReadResponseAsync(
            HttpRequestSettings settings,
            HttpResponseMessage response,
            int attempt,
            CancellationToken cancellationToken)
        {
            ValidateResponseHeaders(response, settings.MaximumHeaderBytes);

            long? contentLength = response.Content == null
                ? null
                : response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > settings.MaximumResponseBytes)
            {
                return HttpResult.Failure(
                    HttpFailureKind.ResponseTooLarge,
                    "Response exceeds the configured size limit.",
                    attempt);
            }

            byte[] responseBytes = response.Content == null
                ? Array.Empty<byte>()
                : await ReadBoundedAsync(
                    response.Content,
                    settings.MaximumResponseBytes,
                    cancellationToken).ConfigureAwait(false);

            HttpResult result = new HttpResult
            {
                AttemptCount = attempt,
                FailureKind = HttpFailureKind.None,
                Header = CopyResponseHeaders(response),
                ResponseUri = response.RequestMessage == null
                    || response.RequestMessage.RequestUri == null
                        ? settings.Uri.AbsoluteUri
                        : response.RequestMessage.RequestUri.AbsoluteUri,
                StatusCode = response.StatusCode,
                StatusDescription = string.IsNullOrWhiteSpace(response.ReasonPhrase)
                    ? response.StatusCode.ToString()
                    : response.ReasonPhrase
            };

            IEnumerable<string> cookies;
            if (response.Headers.TryGetValues("Set-Cookie", out cookies))
            {
                result.Cookie = string.Join(", ", cookies);
                if (settings.ResultCookieType == ResultCookieType.CookieCollection)
                    result.CookieCollection = ParseCookies(new Uri(result.ResponseUri), cookies);
            }

            if (settings.ResultType == ResultType.Byte)
            {
                result.ResultByte = responseBytes;
                result.Html = string.Empty;
            }
            else
            {
                Encoding encoding = ResolveEncoding(settings, response);
                result.Html = encoding.GetString(responseBytes);
                if (settings.IsToLower)
                    result.Html = result.Html.ToLowerInvariant();
            }

            return result;
        }

        private static CookieCollection ParseCookies(Uri requestUri, IEnumerable<string> values)
        {
            CookieContainer container = new CookieContainer();
            foreach (string value in values)
            {
                try
                {
                    container.SetCookies(requestUri, value);
                }
                catch (CookieException)
                {
                    // Preserve the HTTP response when an individual provider cookie is malformed.
                }
            }

            return container.GetCookies(requestUri);
        }

        private static async Task<byte[]> ReadBoundedAsync(
            HttpContent content,
            int maximumBytes,
            CancellationToken cancellationToken)
        {
            using (Stream input = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (MemoryStream output = new MemoryStream(Math.Min(maximumBytes, 81920)))
            {
                byte[] buffer = new byte[81920];
                while (true)
                {
                    int count = await input.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        cancellationToken).ConfigureAwait(false);
                    if (count == 0)
                        return output.ToArray();
                    if (output.Length > maximumBytes - count)
                        throw new ResponseSizeLimitException();

                    output.Write(buffer, 0, count);
                }
            }
        }

        private static void ValidateResponseHeaders(HttpResponseMessage response, int maximumBytes)
        {
            int totalBytes = 0;
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                AddHeaderBytes(header, ref totalBytes, maximumBytes);
            if (response.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                    AddHeaderBytes(header, ref totalBytes, maximumBytes);
            }
        }

        private static void AddHeaderBytes(
            KeyValuePair<string, IEnumerable<string>> header,
            ref int totalBytes,
            int maximumBytes)
        {
            AddBoundedByteCount(Encoding.UTF8.GetByteCount(header.Key), ref totalBytes, maximumBytes);
            foreach (string value in header.Value)
                AddBoundedByteCount(Encoding.UTF8.GetByteCount(value ?? string.Empty), ref totalBytes, maximumBytes);
        }

        private static void AddBoundedByteCount(int byteCount, ref int totalBytes, int maximumBytes)
        {
            if (byteCount > maximumBytes - totalBytes)
                throw new ResponseHeaderLimitException();
            totalBytes += byteCount;
        }

        private static WebHeaderCollection CopyResponseHeaders(HttpResponseMessage response)
        {
            WebHeaderCollection headers = new WebHeaderCollection();
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                headers[header.Key] = string.Join(", ", header.Value);
            if (response.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                    headers[header.Key] = string.Join(", ", header.Value);
            }

            return headers;
        }

        private static Encoding ResolveEncoding(HttpRequestSettings settings, HttpResponseMessage response)
        {
            if (settings.Encoding != null)
                return settings.Encoding;

            string charset = response.Content == null || response.Content.Headers.ContentType == null
                ? null
                : response.Content.Headers.ContentType.CharSet;
            if (!string.IsNullOrWhiteSpace(charset))
            {
                try
                {
                    return Encoding.GetEncoding(charset.Trim('"', '\''));
                }
                catch (ArgumentException)
                {
                    // Fall back to UTF-8 when a provider declares an unknown character set.
                }
            }

            return Encoding.UTF8;
        }

        private static bool ShouldRetry(HttpRequestSettings settings, HttpStatusCode statusCode)
        {
            int code = (int)statusCode;
            return settings.CanRetry && (code == 429 || code >= 500 && code <= 599);
        }

        private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
        {
            if (response != null && response.Headers.RetryAfter != null)
            {
                TimeSpan? retryAfter = response.Headers.RetryAfter.Delta;
                if (!retryAfter.HasValue && response.Headers.RetryAfter.Date.HasValue)
                    retryAfter = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                if (retryAfter.HasValue)
                    return BoundRetryDelay(retryAfter.Value);
            }

            double milliseconds = Math.Min(5000, 250 * Math.Pow(2, Math.Max(0, attempt - 1)));
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        private static TimeSpan BoundRetryDelay(TimeSpan delay)
        {
            if (delay < TimeSpan.Zero)
                return TimeSpan.Zero;
            return delay > MaximumRetryDelay ? MaximumRetryDelay : delay;
        }

        private async Task<HttpResult> DelayForRetryAsync(
            TimeSpan delay,
            int attempt,
            CancellationToken callerToken,
            CancellationToken operationToken)
        {
            try
            {
                await _delayAsync(delay, operationToken).ConfigureAwait(false);
                return null;
            }
            catch (OperationCanceledException)
            {
                return callerToken.IsCancellationRequested
                    ? HttpResult.Failure(HttpFailureKind.Cancelled, "Request cancelled.", attempt)
                    : HttpResult.Failure(HttpFailureKind.Timeout, "Request timed out.", attempt);
            }
        }

        private static bool IsConfigurationException(Exception exception)
        {
            return exception is ArgumentException ||
                exception is FormatException ||
                exception is InvalidOperationException ||
                exception is NotSupportedException ||
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is System.Security.Cryptography.CryptographicException;
        }

        private static bool IsTlsValidationFailure(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is System.Security.Authentication.AuthenticationException)
                    return true;
                WebException webException = current as WebException;
                if (webException != null &&
                    (webException.Status == WebExceptionStatus.TrustFailure ||
                     webException.Status == WebExceptionStatus.SecureChannelFailure))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ResponseSizeLimitException : Exception
        {
        }

        private sealed class ResponseHeaderLimitException : Exception
        {
        }

        private sealed class HttpRequestSettings
        {
            private HttpRequestSettings()
            {
            }

            internal Uri Uri { get; private set; }
            internal HttpMethod Method { get; private set; }
            internal TimeSpan Timeout { get; private set; }
            internal int MaximumResponseBytes { get; private set; }
            internal int MaximumHeaderBytes { get; private set; }
            internal int MaximumRetries { get; private set; }
            internal bool CanRetry { get; private set; }
            internal bool HasRequestBody { get; private set; }
            internal PostDataType PostDataType { get; private set; }
            internal string PostData { get; private set; }
            internal byte[] PostDataBytes { get; private set; }
            internal Encoding PostEncoding { get; private set; }
            internal string ContentType { get; private set; }
            internal IReadOnlyList<KeyValuePair<string, string>> Headers { get; private set; }
            internal string Accept { get; private set; }
            internal string UserAgent { get; private set; }
            internal string Cookie { get; private set; }
            internal string Referer { get; private set; }
            internal string Host { get; private set; }
            internal DateTimeOffset? IfModifiedSince { get; private set; }
            internal bool Expect100Continue { get; private set; }
            internal bool KeepAlive { get; private set; }
            internal Version ProtocolVersion { get; private set; }
            internal ResultType ResultType { get; private set; }
            internal ResultCookieType ResultCookieType { get; private set; }
            internal Encoding Encoding { get; private set; }
            internal bool IsToLower { get; private set; }
            internal HttpClientConfiguration ClientConfiguration { get; private set; }

            internal static HttpRequestSettings Create(HttpItem item)
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));
                Uri uri;
                if (!Uri.TryCreate(item.URL, UriKind.Absolute, out uri) ||
                    uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new ArgumentException("The request URL must be absolute HTTP or HTTPS.", nameof(item));
                }
                if (!string.IsNullOrEmpty(uri.UserInfo))
                    throw new ArgumentException("Credentials must not be embedded in the request URL.", nameof(item));
                if (item.IPEndPoint != null)
                    throw new NotSupportedException("Binding a shared HTTP client to a local endpoint is unsupported.");

                int timeoutMilliseconds = GetTimeoutMilliseconds(item.Timeout, item.ReadWriteTimeout);
                int maximumResponseBytes = RequireRange(
                    item.MaximumResponseBytes,
                    1,
                    AbsoluteMaximumBodyBytes,
                    nameof(item.MaximumResponseBytes));
                int maximumRequestBytes = RequireRange(
                    item.MaximumRequestBytes,
                    1,
                    AbsoluteMaximumBodyBytes,
                    nameof(item.MaximumRequestBytes));
                int maximumHeaderBytes = RequireRange(
                    item.MaximumHeaderBytes,
                    1024,
                    AbsoluteMaximumHeaderBytes,
                    nameof(item.MaximumHeaderBytes));
                int maximumRetries = RequireRange(
                    item.MaxRetries,
                    0,
                    AbsoluteMaximumRetries,
                    nameof(item.MaxRetries));

                HttpMethod method = CreateMethod(item.Method);
                ValidateRequestBody(item, method, maximumRequestBytes);
                IReadOnlyList<KeyValuePair<string, string>> headers = ReadHeaders(
                    item.Header,
                    maximumHeaderBytes);
                bool safeMethod = method == HttpMethod.Get ||
                    method == HttpMethod.Head ||
                    method == HttpMethod.Options ||
                    method == HttpMethod.Trace;

                return new HttpRequestSettings
                {
                    Accept = item.Accept,
                    CanRetry = safeMethod || item.AllowUnsafeRetries,
                    ClientConfiguration = HttpClientConfiguration.Create(item),
                    ContentType = item.ContentType,
                    Cookie = CreateCookieHeader(item, uri),
                    Encoding = item.Encoding,
                    Expect100Continue = item.Expect100Continue,
                    HasRequestBody = method != HttpMethod.Get && method != HttpMethod.Head,
                    Headers = headers,
                    Host = item.Host,
                    IfModifiedSince = item.IfModifiedSince.HasValue
                        ? new DateTimeOffset(item.IfModifiedSince.Value)
                        : (DateTimeOffset?)null,
                    IsToLower = item.IsToLower,
                    KeepAlive = item.KeepAlive,
                    MaximumHeaderBytes = maximumHeaderBytes,
                    MaximumResponseBytes = maximumResponseBytes,
                    MaximumRetries = maximumRetries,
                    Method = method,
                    PostData = item.Postdata,
                    PostDataBytes = item.PostdataByte,
                    PostDataType = item.PostDataType,
                    PostEncoding = item.PostEncoding ?? Encoding.UTF8,
                    ProtocolVersion = item.ProtocolVersion ?? HttpVersion.Version11,
                    Referer = item.Referer,
                    ResultType = item.ResultType,
                    ResultCookieType = item.ResultCookieType,
                    Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
                    Uri = uri,
                    UserAgent = item.UserAgent
                };
            }

            private static int GetTimeoutMilliseconds(int timeout, int readWriteTimeout)
            {
                int requestTimeout = RequireRange(timeout, 1, 30 * 60 * 1000, nameof(HttpItem.Timeout));
                int streamTimeout = RequireRange(
                    readWriteTimeout,
                    1,
                    30 * 60 * 1000,
                    nameof(HttpItem.ReadWriteTimeout));
                return Math.Min(requestTimeout, streamTimeout);
            }

            private static int RequireRange(int value, int minimum, int maximum, string name)
            {
                if (value < minimum || value > maximum)
                    throw new ArgumentOutOfRangeException(name);
                return value;
            }

            private static HttpMethod CreateMethod(string value)
            {
                if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
                    throw new ArgumentException("The HTTP method is invalid.", nameof(value));
                return new HttpMethod(value.Trim().ToUpperInvariant());
            }

            private static void ValidateRequestBody(HttpItem item, HttpMethod method, int maximumBytes)
            {
                if (method == HttpMethod.Get || method == HttpMethod.Head)
                    return;

                long byteCount;
                switch (item.PostDataType)
                {
                    case PostDataType.Byte:
                        byteCount = item.PostdataByte == null ? 0 : item.PostdataByte.LongLength;
                        break;
                    case PostDataType.FilePath:
                        if (string.IsNullOrWhiteSpace(item.Postdata))
                            throw new ArgumentException("A request body file path is required.", nameof(item));
                        byteCount = new FileInfo(item.Postdata).Length;
                        break;
                    default:
                        byteCount = (item.PostEncoding ?? Encoding.UTF8)
                            .GetByteCount(item.Postdata ?? string.Empty);
                        break;
                }

                if (byteCount > maximumBytes)
                    throw new ArgumentException("The request body exceeds the configured size limit.", nameof(item));
            }

            private static IReadOnlyList<KeyValuePair<string, string>> ReadHeaders(
                WebHeaderCollection headers,
                int maximumBytes)
            {
                List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
                if (headers == null)
                    return result;
                if (headers.Count > 100)
                    throw new ArgumentException("The request contains too many headers.", nameof(headers));

                int totalBytes = 0;
                foreach (string name in headers.AllKeys)
                {
                    string value = headers[name] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name) ||
                        name.Any(char.IsControl) ||
                        value.Any(IsInvalidHeaderCharacter))
                    {
                        throw new ArgumentException("The request contains an invalid header.", nameof(headers));
                    }
                    totalBytes = checked(
                        totalBytes + Encoding.UTF8.GetByteCount(name) + Encoding.UTF8.GetByteCount(value));
                    if (totalBytes > maximumBytes)
                        throw new ArgumentException(
                            "The request headers exceed the configured size limit.",
                            nameof(headers));
                    result.Add(new KeyValuePair<string, string>(name, value));
                }

                return result;
            }

            private static string CreateCookieHeader(HttpItem item, Uri uri)
            {
                List<string> values = new List<string>();
                if (!string.IsNullOrWhiteSpace(item.Cookie))
                    values.Add(item.Cookie);
                if (item.ResultCookieType == ResultCookieType.CookieCollection &&
                    item.CookieCollection != null &&
                    item.CookieCollection.Count > 0)
                {
                    CookieContainer container = new CookieContainer();
                    container.Add(item.CookieCollection);
                    string collectionHeader = container.GetCookieHeader(uri);
                    if (!string.IsNullOrWhiteSpace(collectionHeader))
                        values.Add(collectionHeader);
                }

                return string.Join("; ", values);
            }

            private static bool IsInvalidHeaderCharacter(char value)
            {
                return value == '\r' || value == '\n' || value == '\0';
            }
        }

        private sealed class HttpClientConfiguration : IEquatable<HttpClientConfiguration>
        {
            private HttpClientConfiguration()
            {
            }

            internal IWebProxy Proxy { get; private set; }
            internal ICredentials Credentials { get; private set; }
            internal bool AllowAutoRedirect { get; private set; }
            internal int MaximumAutomaticRedirections { get; private set; }
            internal int ConnectionLimit { get; private set; }
            internal string ClientCertificatePath { get; private set; }
            internal long ClientCertificateFileVersion { get; private set; }
            internal X509Certificate[] ClientCertificates { get; private set; }
            internal string ClientCertificateSignature { get; private set; }

            internal static HttpClientConfiguration Create(HttpItem item)
            {
                string certificatePath = null;
                long certificateFileVersion = 0;
                if (!string.IsNullOrWhiteSpace(item.CerPath))
                {
                    certificatePath = Path.GetFullPath(item.CerPath);
                    FileInfo certificateFile = new FileInfo(certificatePath);
                    if (!certificateFile.Exists)
                        throw new FileNotFoundException("The client certificate file does not exist.");
                    certificateFileVersion = certificateFile.LastWriteTimeUtc.Ticks ^ certificateFile.Length;
                }

                X509Certificate[] certificates = item.ClentCertificates == null
                    ? Array.Empty<X509Certificate>()
                    : item.ClentCertificates.Cast<X509Certificate>().ToArray();
                string signature = string.Join(
                    ";",
                    certificates.Select(certificate => certificate.GetCertHashString()));

                return new HttpClientConfiguration
                {
                    AllowAutoRedirect = item.Allowautoredirect,
                    ClientCertificateFileVersion = certificateFileVersion,
                    ClientCertificatePath = certificatePath,
                    ClientCertificates = certificates,
                    ClientCertificateSignature = signature,
                    ConnectionLimit = RequireRange(
                        item.Connectionlimit > 0 ? item.Connectionlimit : 100,
                        1,
                        100000,
                        nameof(item.Connectionlimit)),
                    Credentials = item.ICredentials,
                    MaximumAutomaticRedirections = item.MaximumAutomaticRedirections,
                    Proxy = item.WebProxy
                };
            }

            private static int RequireRange(int value, int minimum, int maximum, string name)
            {
                if (value < minimum || value > maximum)
                    throw new ArgumentOutOfRangeException(name);
                return value;
            }

            public bool Equals(HttpClientConfiguration other)
            {
                return other != null &&
                    ReferenceEquals(Proxy, other.Proxy) &&
                    ReferenceEquals(Credentials, other.Credentials) &&
                    AllowAutoRedirect == other.AllowAutoRedirect &&
                    MaximumAutomaticRedirections == other.MaximumAutomaticRedirections &&
                    ConnectionLimit == other.ConnectionLimit &&
                    ClientCertificateFileVersion == other.ClientCertificateFileVersion &&
                    string.Equals(
                        ClientCertificatePath,
                        other.ClientCertificatePath,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        ClientCertificateSignature,
                        other.ClientCertificateSignature,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as HttpClientConfiguration);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Proxy == null ? 0 : RuntimeHelpers.GetHashCode(Proxy);
                    hash = hash * 397 ^ (Credentials == null ? 0 : RuntimeHelpers.GetHashCode(Credentials));
                    hash = hash * 397 ^ AllowAutoRedirect.GetHashCode();
                    hash = hash * 397 ^ MaximumAutomaticRedirections;
                    hash = hash * 397 ^ ConnectionLimit;
                    hash = hash * 397 ^ ClientCertificateFileVersion.GetHashCode();
                    hash = hash * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(
                        ClientCertificatePath ?? string.Empty);
                    hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(
                        ClientCertificateSignature ?? string.Empty);
                    return hash;
                }
            }
        }
    }

    /// <summary>
    /// Describes one bounded HTTP request without changing process-wide network state.
    /// </summary>
    public class HttpItem
    {
        /// <summary>Identifies the absolute HTTP or HTTPS request target.</summary>
        public string URL { get; set; }

        /// <summary>Specifies the HTTP method sent to the request target.</summary>
        public string Method { get; set; } = "GET";

        /// <summary>Limits the total request duration in milliseconds.</summary>
        public int Timeout { get; set; } = 300000;

        /// <summary>Limits response-stream work in milliseconds.</summary>
        public int ReadWriteTimeout { get; set; } = 300000;

        /// <summary>Overrides the Host header when a provider requires a specific authority.</summary>
        public string Host { get; set; }

        /// <summary>Controls whether the connection may remain open for reuse.</summary>
        public bool KeepAlive { get; set; } = true;

        /// <summary>Specifies the accepted response media types.</summary>
        public string Accept { get; set; } = "text/html, application/xhtml+xml, */*";

        /// <summary>Specifies the request body media type.</summary>
        public string ContentType { get; set; } = "text/html";

        /// <summary>Identifies the caller in the User-Agent request header.</summary>
        public string UserAgent { get; set; } =
            "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0)";

        /// <summary>Overrides response character-set detection when supplied.</summary>
        public Encoding Encoding { get; set; }

        /// <summary>Selects how request body data is supplied.</summary>
        public PostDataType PostDataType { get; set; } = PostDataType.String;

        /// <summary>Contains the text body or file path selected by <see cref="PostDataType"/>.</summary>
        public string Postdata { get; set; }

        /// <summary>Contains the request body used for <see cref="PostDataType.Byte"/>.</summary>
        public byte[] PostdataByte { get; set; }

        /// <summary>Provides caller-owned cookies to select for the request target.</summary>
        public CookieCollection CookieCollection { get; set; }

        /// <summary>Provides a preformatted Cookie request header.</summary>
        public string Cookie { get; set; }

        /// <summary>Identifies the absolute referring URI.</summary>
        public string Referer { get; set; }

        /// <summary>Identifies an optional client certificate file to attach.</summary>
        /// <remarks>The certificate does not alter server certificate validation.</remarks>
        public string CerPath { get; set; }

        /// <summary>Controls whether decoded response text is converted to invariant lower case.</summary>
        public bool IsToLower { get; set; }

        /// <summary>Controls whether the transport follows redirects automatically.</summary>
        public bool Allowautoredirect { get; set; }

        /// <summary>Limits concurrent connections for this transport configuration.</summary>
        public int Connectionlimit { get; set; } = 99999;

        /// <summary>Routes the request through an optional proxy.</summary>
        public IWebProxy WebProxy { get; set; }

        /// <summary>Selects whether the response is returned as text or bytes.</summary>
        public ResultType ResultType { get; set; } = ResultType.String;

        /// <summary>Provides additional request headers after validation.</summary>
        public WebHeaderCollection Header { get; set; } = new WebHeaderCollection();

        /// <summary>Selects the HTTP protocol version.</summary>
        public Version ProtocolVersion { get; set; }

        /// <summary>Controls whether an Expect: 100-continue header is sent.</summary>
        public bool Expect100Continue { get; set; }

        /// <summary>Provides caller-owned client certificates to attach.</summary>
        /// <remarks>
        /// The collection and certificates remain owned by the caller and must remain valid for the lifetime of
        /// the provider transport.
        /// </remarks>
        public X509CertificateCollection ClentCertificates { get; set; }

        /// <summary>Selects the encoding used for string request content.</summary>
        public Encoding PostEncoding { get; set; }

        /// <summary>Selects the cookie result representation.</summary>
        public ResultCookieType ResultCookieType { get; set; } = ResultCookieType.String;

        /// <summary>Provides credentials used after a server authentication challenge.</summary>
        public ICredentials ICredentials { get; set; } = CredentialCache.DefaultCredentials;

        /// <summary>Limits redirects when automatic redirects are enabled.</summary>
        public int MaximumAutomaticRedirections { get; set; }

        /// <summary>Provides the If-Modified-Since request value.</summary>
        public DateTime? IfModifiedSince { get; set; }

        /// <summary>Provides a legacy local endpoint binding.</summary>
        /// <remarks>Shared HttpClient transports reject this unsupported setting.</remarks>
        public IPEndPoint IPEndPoint { get; set; }

        /// <summary>Limits the decompressed response body size in bytes.</summary>
        public int MaximumResponseBytes { get; set; } = 8 * 1024 * 1024;

        /// <summary>Limits the request body size in bytes.</summary>
        public int MaximumRequestBytes { get; set; } = 8 * 1024 * 1024;

        /// <summary>Limits combined request or response headers in bytes.</summary>
        public int MaximumHeaderBytes { get; set; } = 64 * 1024;

        /// <summary>Limits transient retries after the initial attempt.</summary>
        public int MaxRetries { get; set; } = 2;

        /// <summary>Allows explicit retries for non-idempotent methods.</summary>
        public bool AllowUnsafeRetries { get; set; }
    }

    /// <summary>
    /// Contains a bounded HTTP response and structured failure information.
    /// </summary>
    public class HttpResult
    {
        /// <summary>Contains the Set-Cookie response header.</summary>
        public string Cookie { get; set; }

        /// <summary>Contains parsed response cookies when requested and available.</summary>
        public CookieCollection CookieCollection { get; set; }

        /// <summary>Contains decoded response text.</summary>
        public string Html { get; set; } = string.Empty;

        /// <summary>Contains response bytes when byte mode was requested.</summary>
        public byte[] ResultByte { get; set; }

        /// <summary>Contains bounded response headers.</summary>
        public WebHeaderCollection Header { get; set; }

        /// <summary>Describes the response or structured failure without sensitive details.</summary>
        public string StatusDescription { get; set; }

        /// <summary>Identifies the HTTP response status.</summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>Identifies the final response URI after redirects.</summary>
        public string ResponseUri { get; set; }

        /// <summary>Identifies a structured transport failure.</summary>
        public HttpFailureKind FailureKind { get; set; }

        /// <summary>Counts network attempts made for this result.</summary>
        public int AttemptCount { get; set; }

        /// <summary>Indicates whether transport completed and returned an HTTP response.</summary>
        public bool TransportSucceeded => FailureKind == HttpFailureKind.None;

        /// <summary>Indicates whether the response status is between 200 and 299.</summary>
        public bool IsSuccessStatusCode => TransportSucceeded &&
            (int)StatusCode >= 200 &&
            (int)StatusCode <= 299;

        /// <summary>Resolves an absolute redirect target from a valid Location header.</summary>
        public string RedirectUrl
        {
            get
            {
                string location = Header == null ? null : Header["Location"];
                if (string.IsNullOrWhiteSpace(location))
                    return string.Empty;

                Uri redirect;
                if (Uri.TryCreate(location, UriKind.Absolute, out redirect))
                    return redirect.AbsoluteUri;
                Uri responseUri;
                if (Uri.TryCreate(ResponseUri, UriKind.Absolute, out responseUri) &&
                    Uri.TryCreate(responseUri, location, out redirect))
                {
                    return redirect.AbsoluteUri;
                }

                return string.Empty;
            }
        }

        internal static HttpResult Failure(HttpFailureKind kind, string message, int attemptCount)
        {
            return new HttpResult
            {
                AttemptCount = attemptCount,
                FailureKind = kind,
                Html = string.Empty,
                StatusDescription = message
            };
        }
    }

    /// <summary>Specifies whether a response is decoded as text or retained as bytes.</summary>
    public enum ResultType
    {
        /// <summary>Decodes the bounded body as text.</summary>
        String,
        /// <summary>Returns the bounded body as bytes.</summary>
        Byte
    }

    /// <summary>Specifies how request body content is supplied.</summary>
    public enum PostDataType
    {
        /// <summary>Encodes <see cref="HttpItem.Postdata"/> as text.</summary>
        String,
        /// <summary>Uses <see cref="HttpItem.PostdataByte"/>.</summary>
        Byte,
        /// <summary>Streams the file named by <see cref="HttpItem.Postdata"/>.</summary>
        FilePath
    }

    /// <summary>Specifies the retained cookie representation.</summary>
    public enum ResultCookieType
    {
        /// <summary>Returns the Set-Cookie header as a string.</summary>
        String,
        /// <summary>Requests a cookie collection when the transport can provide one.</summary>
        CookieCollection
    }

    /// <summary>Identifies a structured HTTP transport failure.</summary>
    public enum HttpFailureKind
    {
        /// <summary>No transport failure occurred.</summary>
        None,
        /// <summary>Request configuration was invalid.</summary>
        Configuration,
        /// <summary>The caller cancelled the operation.</summary>
        Cancelled,
        /// <summary>The configured timeout elapsed.</summary>
        Timeout,
        /// <summary>The network operation failed.</summary>
        Network,
        /// <summary>Platform server-certificate validation failed.</summary>
        TlsValidation,
        /// <summary>The response body exceeded its configured limit.</summary>
        ResponseTooLarge,
        /// <summary>The response headers exceeded their configured limit.</summary>
        HeadersTooLarge,
        /// <summary>The response stream or compression was malformed.</summary>
        MalformedResponse
    }
}
