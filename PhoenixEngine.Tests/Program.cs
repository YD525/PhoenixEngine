using PhoenixEngine.Request;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PhoenixEngine.Tests
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            KeyValuePair<string, Func<Task>>[] tests =
            {
                SyncTest(
                    nameof(CompletesValidTlsHandshakeAndConfiguresClientCertificates),
                    CompletesValidTlsHandshakeAndConfiguresClientCertificates),
                SyncTest(nameof(RejectsExpiredCertificate), RejectsExpiredCertificate),
                SyncTest(
                    nameof(RejectsWrongHostCertificateWhenClientCertificatePathIsConfigured),
                    RejectsWrongHostCertificateWhenClientCertificatePathIsConfigured),
                SyncTest(
                    nameof(RejectsUntrustedSelfSignedCertificate),
                    RejectsUntrustedSelfSignedCertificate),
                AsyncTest(nameof(ReturnsSuccessfulResponseAsync), ReturnsSuccessfulResponseAsync),
                AsyncTest(nameof(ReportsTimeoutAsync), ReportsTimeoutAsync),
                AsyncTest(nameof(ReportsCancellationAsync), ReportsCancellationAsync),
                AsyncTest(
                    nameof(RetriesRateLimitAndServerFailuresAsync),
                    RetriesRateLimitAndServerFailuresAsync),
                AsyncTest(nameof(DoesNotRetryUnsafePostAsync), DoesNotRetryUnsafePostAsync),
                AsyncTest(nameof(RejectsOversizedResponseAsync), RejectsOversizedResponseAsync),
                AsyncTest(nameof(RejectsOversizedStreamAsync), RejectsOversizedStreamAsync),
                AsyncTest(nameof(RejectsOversizedHeadersAsync), RejectsOversizedHeadersAsync),
                AsyncTest(nameof(CancelsDuringNetworkRetryDelayAsync), CancelsDuringNetworkRetryDelayAsync),
                AsyncTest(nameof(PreservesMalformedBodyAsync), PreservesMalformedBodyAsync)
            };

            int failures = 0;
            foreach (KeyValuePair<string, Func<Task>> test in tests)
            {
                try
                {
                    await test.Value().ConfigureAwait(false);
                    Console.WriteLine("Passed {0}", test.Key);
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine("Failed {0}: {1}", test.Key, exception);
                }
            }

            Console.WriteLine("{0} transport tests passed; {1} failed.", tests.Length - failures, failures);
            return failures == 0 ? 0 : 1;
        }

        private static KeyValuePair<string, Func<Task>> SyncTest(string name, Action test)
        {
            return new KeyValuePair<string, Func<Task>>(
                name,
                () =>
                {
                    test();
                    return Task.CompletedTask;
                });
        }

        private static KeyValuePair<string, Func<Task>> AsyncTest(string name, Func<Task> test)
        {
            return new KeyValuePair<string, Func<Task>>(name, test);
        }

        private static void CompletesValidTlsHandshakeAndConfiguresClientCertificates()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            using (X509Certificate2 authority = CreateCertificateAuthority(now.AddDays(-2), now.AddDays(30)))
            using (X509Certificate2 serverCertificate = CreateServerCertificate(
                authority,
                "localhost",
                now.AddDays(-1),
                now.AddDays(2)))
            using (X509Certificate2 clientCertificate = CreateSelfSignedServerCertificate(
                "client.local",
                now.AddDays(-1),
                now.AddDays(2)))
            {
                string clientCertificatePath = WriteClientCertificate(clientCertificate);
                try
                {
                    CompleteTrustedTestHandshake(serverCertificate, authority);
                    AssertClientCertificatesConfigured(
                        clientCertificate,
                        clientCertificatePath);
                }
                finally
                {
                    File.Delete(clientCertificatePath);
                }
            }
        }

        private static void RejectsExpiredCertificate()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            using (X509Certificate2 authority = CreateCertificateAuthority(now.AddDays(-30), now.AddDays(30)))
            using (X509Certificate2 serverCertificate = CreateServerCertificate(
                authority,
                "localhost",
                now.AddDays(-10),
                now.AddDays(-2)))
            {
                AssertTlsFailure(SendRequest(serverCertificate, null, null), serverCertificate);
            }
        }

        private static void RejectsWrongHostCertificateWhenClientCertificatePathIsConfigured()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            using (X509Certificate2 authority = CreateCertificateAuthority(now.AddDays(-2), now.AddDays(30)))
            using (X509Certificate2 serverCertificate = CreateServerCertificate(
                authority,
                "wrong-host.invalid",
                now.AddDays(-1),
                now.AddDays(2)))
            using (X509Certificate2 clientCertificate = CreateSelfSignedServerCertificate(
                "client.local",
                now.AddDays(-1),
                now.AddDays(2)))
            {
                string clientCertificatePath = WriteClientCertificate(clientCertificate);
                try
                {
                    AssertTlsFailure(
                        SendRequest(serverCertificate, null, clientCertificatePath),
                        serverCertificate,
                        clientCertificatePath);
                }
                finally
                {
                    File.Delete(clientCertificatePath);
                }
            }
        }

        private static void RejectsUntrustedSelfSignedCertificate()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            using (X509Certificate2 serverCertificate = CreateSelfSignedServerCertificate(
                "localhost",
                now.AddDays(-1),
                now.AddDays(2)))
            {
                AssertTlsFailure(SendRequest(serverCertificate, null, null), serverCertificate);
            }
        }

        private static HttpResult SendRequest(
            X509Certificate2 serverCertificate,
            X509CertificateCollection clientCertificates,
            string clientCertificatePath)
        {
            RemoteCertificateValidationCallback originalCallback =
                ServicePointManager.ServerCertificateValidationCallback;
            if (originalCallback != null)
                throw new InvalidOperationException("The test process must begin with the platform TLS policy.");

            using (TlsTestServer server = new TlsTestServer(serverCertificate))
            {
                HttpItem item = new HttpItem
                {
                    URL = server.Address.AbsoluteUri,
                    Encoding = Encoding.UTF8,
                    Timeout = 5000,
                    ReadWriteTimeout = 5000,
                    ClentCertificates = clientCertificates,
                    CerPath = clientCertificatePath
                };
                HttpResult result = new HttpHelper().GetHtml(item);

                if (!ReferenceEquals(
                    originalCallback,
                    ServicePointManager.ServerCertificateValidationCallback))
                {
                    throw new InvalidOperationException(
                        "The request changed the process-wide TLS validation callback.");
                }

                return result;
            }
        }

        private static void CompleteTrustedTestHandshake(
            X509Certificate2 serverCertificate,
            X509Certificate2 authority)
        {
            using (TlsTestServer server = new TlsTestServer(serverCertificate))
            using (TcpClient client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, server.Address.Port);
                RemoteCertificateValidationCallback validation = (sender, certificate, chain, errors) =>
                    IsValidTestCertificate(certificate, errors, authority);
                using (SslStream stream = new SslStream(client.GetStream(), false, validation))
                {
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;
                    stream.AuthenticateAsClient(
                        "localhost",
                        new X509CertificateCollection(),
                        SslProtocols.Tls12,
                        false);
                    byte[] request = Encoding.ASCII.GetBytes(
                        "GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n");
                    stream.Write(request, 0, request.Length);
                    stream.Flush();

                    using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                    {
                        string response = reader.ReadToEnd();
                        if (!response.EndsWith("\r\n\r\nok", StringComparison.Ordinal))
                            throw new InvalidOperationException("The valid TLS fixture did not complete its response.");
                    }
                }
            }
        }

        private static bool IsValidTestCertificate(
            X509Certificate certificate,
            SslPolicyErrors errors,
            X509Certificate2 authority)
        {
            if (certificate == null ||
                (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0 ||
                (errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
            {
                return false;
            }

            using (X509Certificate2 presented = new X509Certificate2(certificate))
            using (X509Chain chain = new X509Chain())
            {
                chain.ChainPolicy.ExtraStore.Add(authority);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                if (!chain.Build(presented) || chain.ChainElements.Count == 0)
                    return false;

                X509Certificate2 root = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                return string.Equals(root.Thumbprint, authority.Thumbprint, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void AssertClientCertificatesConfigured(
            X509Certificate2 clientCertificate,
            string clientCertificatePath)
        {
            RemoteCertificateValidationCallback originalCallback =
                ServicePointManager.ServerCertificateValidationCallback;
            HttpItem item = new HttpItem
            {
                URL = "http://127.0.0.1/",
                ClentCertificates = new X509CertificateCollection { clientCertificate },
                CerPath = clientCertificatePath
            };

            using (HttpClientHandler handler = HttpHelper.CreateHandlerForTesting(item))
            {
                AssertEqual(
                    2,
                    handler.ClientCertificates.Count,
                    "Both client certificate sources must be attached.");
                if (!ReferenceEquals(originalCallback, ServicePointManager.ServerCertificateValidationCallback))
                {
                    throw new InvalidOperationException(
                        "Client certificates changed the process-wide TLS policy.");
                }
            }
        }

        private static async Task ReturnsSuccessfulResponseAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(
                (request, token) => Task.FromResult(CreateResponse(HttpStatusCode.OK, "success"))))
            using (HttpClient client = CreateClient(handler))
            {
                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    CreateHttpItem(),
                    CancellationToken.None).ConfigureAwait(false);

                AssertEqual(true, result.IsSuccessStatusCode, "A 200 response must succeed.");
                AssertEqual("success", result.Html, "The response body must be preserved.");
                AssertEqual(1, result.AttemptCount, "A successful response must use one attempt.");
            }
        }

        private static async Task ReportsTimeoutAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(WaitForCancellationAsync))
            using (HttpClient client = CreateClient(handler))
            {
                HttpItem item = CreateHttpItem();
                item.Timeout = 50;
                item.ReadWriteTimeout = 50;
                Stopwatch elapsed = Stopwatch.StartNew();

                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    item,
                    CancellationToken.None).ConfigureAwait(false);
                elapsed.Stop();

                AssertEqual(HttpFailureKind.Timeout, result.FailureKind, "An elapsed timeout must be distinct.");
                AssertEqual(1, result.AttemptCount, "A timeout must not be retried.");
                AssertLessThan(TimeSpan.FromSeconds(2), elapsed.Elapsed, "Timeout must not block the caller.");
            }
        }

        private static async Task ReportsCancellationAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(WaitForCancellationAsync))
            using (HttpClient client = CreateClient(handler))
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                cancellation.CancelAfter(50);
                Stopwatch elapsed = Stopwatch.StartNew();
                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    CreateHttpItem(),
                    cancellation.Token).ConfigureAwait(false);
                elapsed.Stop();

                AssertEqual(
                    HttpFailureKind.Cancelled,
                    result.FailureKind,
                    "Caller cancellation must be distinct from timeout.");
                AssertEqual(1, result.AttemptCount, "A cancelled request must not be retried.");
                AssertLessThan(TimeSpan.FromSeconds(2), elapsed.Elapsed, "Cancellation must not block the caller.");
            }
        }

        private static async Task RetriesRateLimitAndServerFailuresAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(
                (request, token) => Task.FromResult(CreateRetryResponse((HttpStatusCode)429)),
                (request, token) => Task.FromResult(CreateResponse(HttpStatusCode.BadGateway, "retry")),
                (request, token) => Task.FromResult(CreateResponse(HttpStatusCode.OK, "recovered"))))
            using (HttpClient client = CreateClient(handler))
            {
                HttpItem item = CreateHttpItem();
                item.MaxRetries = 2;
                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    item,
                    CancellationToken.None).ConfigureAwait(false);

                AssertEqual(true, result.IsSuccessStatusCode, "A safe request must recover from 429 and 5xx.");
                AssertEqual("recovered", result.Html, "The final response must be returned.");
                AssertEqual(3, result.AttemptCount, "Both transient responses must consume retries.");
                AssertEqual(3, handler.CallCount, "The handler must receive exactly three attempts.");
            }
        }

        private static async Task DoesNotRetryUnsafePostAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(
                (request, token) => Task.FromResult(CreateResponse(HttpStatusCode.ServiceUnavailable, "busy"))))
            using (HttpClient client = CreateClient(handler))
            {
                HttpItem item = CreateHttpItem();
                item.Method = "POST";
                item.Postdata = "payload";
                item.MaxRetries = 5;

                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    item,
                    CancellationToken.None).ConfigureAwait(false);

                AssertEqual(HttpStatusCode.ServiceUnavailable, result.StatusCode, "The 503 response must be returned.");
                AssertEqual(1, result.AttemptCount, "POST must not retry without explicit authorization.");
                AssertEqual(1, handler.CallCount, "The unsafe operation must be sent exactly once.");
            }
        }

        private static async Task RejectsOversizedResponseAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(
                (request, token) => Task.FromResult(CreateResponse(HttpStatusCode.OK, "12345"))))
            using (HttpClient client = CreateClient(handler))
            {
                HttpItem item = CreateHttpItem();
                item.MaximumResponseBytes = 4;
                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    item,
                    CancellationToken.None).ConfigureAwait(false);

                AssertEqual(
                    HttpFailureKind.ResponseTooLarge,
                    result.FailureKind,
                    "An oversized response must fail before buffering.");
                AssertEqual(string.Empty, result.Html, "An oversized response must not expose a partial body.");
            }
        }

        private static async Task PreservesMalformedBodyAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(
                (request, token) => Task.FromResult(CreateResponse(HttpStatusCode.OK, "{not-json"))))
            using (HttpClient client = CreateClient(handler))
            {
                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    CreateHttpItem(),
                    CancellationToken.None).ConfigureAwait(false);

                AssertEqual(
                    true,
                    result.IsSuccessStatusCode,
                    "Transport must report the HTTP result independently.");
                AssertEqual(
                    "{not-json",
                    result.Html,
                    "Malformed provider data must remain available for DTO validation.");
            }
        }

        private static async Task RejectsOversizedStreamAsync()
        {
            byte[] body = Encoding.UTF8.GetBytes("12345");
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(
                (request, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new NonSeekableReadStream(body))
                })))
            using (HttpClient client = CreateClient(handler))
            {
                HttpItem item = CreateHttpItem();
                item.MaximumResponseBytes = 4;
                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    item,
                    CancellationToken.None).ConfigureAwait(false);

                AssertEqual(
                    HttpFailureKind.ResponseTooLarge,
                    result.FailureKind,
                    "An unknown-length stream must stop at the configured response limit.");
            }
        }

        private static async Task RejectsOversizedHeadersAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(
                (request, token) =>
                {
                    HttpResponseMessage response = CreateResponse(HttpStatusCode.OK, "success");
                    response.Headers.TryAddWithoutValidation("X-Oversized", new string('a', 1100));
                    return Task.FromResult(response);
                }))
            using (HttpClient client = CreateClient(handler))
            {
                HttpItem item = CreateHttpItem();
                item.MaximumHeaderBytes = 1024;
                HttpResult result = await CreateHttpHelper(client).GetHtmlAsync(
                    item,
                    CancellationToken.None).ConfigureAwait(false);

                AssertEqual(
                    HttpFailureKind.HeadersTooLarge,
                    result.FailureKind,
                    "Oversized response headers must fail before body buffering.");
            }
        }

        private static async Task CancelsDuringNetworkRetryDelayAsync()
        {
            using (ScriptedHttpMessageHandler handler = new ScriptedHttpMessageHandler(
                (request, token) => throw new HttpRequestException("Transient network failure.")))
            using (HttpClient client = CreateClient(handler))
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                cancellation.CancelAfter(50);
                HttpResult result = await new HttpHelper(client, Task.Delay).GetHtmlAsync(
                    CreateHttpItem(),
                    cancellation.Token).ConfigureAwait(false);

                AssertEqual(
                    HttpFailureKind.Cancelled,
                    result.FailureKind,
                    "Cancellation during retry delay must remain a structured result.");
                AssertEqual(1, handler.CallCount, "Cancellation must stop before the second network attempt.");
            }
        }

        private static HttpItem CreateHttpItem()
        {
            return new HttpItem
            {
                URL = "https://provider.test/request",
                Method = "GET",
                Encoding = Encoding.UTF8,
                Timeout = 5000,
                ReadWriteTimeout = 5000
            };
        }

        private static HttpClient CreateClient(HttpMessageHandler handler)
        {
            return new HttpClient(handler, false)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        private static HttpHelper CreateHttpHelper(HttpClient client)
        {
            return new HttpHelper(client, (delay, token) => Task.CompletedTask);
        }

        private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage CreateRetryResponse(HttpStatusCode statusCode)
        {
            HttpResponseMessage response = CreateResponse(statusCode, "retry");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            return response;
        }

        private static async Task<HttpResponseMessage> WaitForCancellationAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("The cancellation delay unexpectedly completed.");
        }

        private static void AssertTlsFailure(
            HttpResult result,
            X509Certificate2 certificate,
            params string[] secrets)
        {
            AssertEqual(
                string.Empty,
                result.Html,
                "A rejected TLS response must not return exception details as content.");
            AssertEqual(
                "TLS validation failed.",
                result.StatusDescription,
                "A rejected certificate needs a safe error.");

            string description = result.StatusDescription ?? string.Empty;
            if (description.IndexOf(certificate.Subject, StringComparison.OrdinalIgnoreCase) >= 0 ||
                description.IndexOf(certificate.Thumbprint, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException("The TLS error exposed certificate details.");
            }

            foreach (string secret in secrets)
            {
                if (!string.IsNullOrEmpty(secret) &&
                    description.IndexOf(secret, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException("The TLS error exposed request credentials.");
                }
            }
        }

        private static string WriteClientCertificate(X509Certificate2 certificate)
        {
            string path = Path.Combine(Path.GetTempPath(), "PhoenixEngine-" + Guid.NewGuid() + ".pfx");
            File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, string.Empty));
            return path;
        }

        private static X509Certificate2 CreateCertificateAuthority(
            DateTimeOffset notBefore,
            DateTimeOffset notAfter)
        {
            using (RSA key = RSA.Create(2048))
            {
                CertificateRequest request = new CertificateRequest(
                    "CN=Phoenix Engine TLS Test Root",
                    key,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    true));
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                using (X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter))
                {
                    return ExportWithPrivateKey(certificate);
                }
            }
        }

        private static X509Certificate2 CreateServerCertificate(
            X509Certificate2 authority,
            string hostName,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter)
        {
            using (RSA key = RSA.Create(2048))
            {
                CertificateRequest request = CreateServerCertificateRequest(hostName, key);
                byte[] serialNumber = new byte[16];
                using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                {
                    random.GetBytes(serialNumber);
                }

                using (X509Certificate2 issued = request.Create(authority, notBefore, notAfter, serialNumber))
                using (X509Certificate2 certificate = issued.CopyWithPrivateKey(key))
                {
                    return ExportWithPrivateKey(certificate);
                }
            }
        }

        private static X509Certificate2 CreateSelfSignedServerCertificate(
            string hostName,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter)
        {
            using (RSA key = RSA.Create(2048))
            {
                CertificateRequest request = CreateServerCertificateRequest(hostName, key);
                using (X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter))
                {
                    return ExportWithPrivateKey(certificate);
                }
            }
        }

        private static CertificateRequest CreateServerCertificateRequest(string hostName, RSA key)
        {
            CertificateRequest request = new CertificateRequest(
                "CN=" + hostName,
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));

            OidCollection usages = new OidCollection
            {
                new Oid("1.3.6.1.5.5.7.3.1")
            };
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));
            SubjectAlternativeNameBuilder names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(hostName);
            request.CertificateExtensions.Add(names.Build());
            return request;
        }

        private static X509Certificate2 ExportWithPrivateKey(X509Certificate2 certificate)
        {
            byte[] exported = certificate.Export(X509ContentType.Pfx, string.Empty);
            return new X509Certificate2(
                exported,
                string.Empty,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(string.Format(
                    "{0} Expected <{1}> but received <{2}>.",
                    message,
                    expected,
                    actual));
            }
        }

        private static void AssertLessThan(TimeSpan maximum, TimeSpan actual, string message)
        {
            if (actual >= maximum)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} Expected less than <{1}> but received <{2}>.",
                    message,
                    maximum,
                    actual));
            }
        }

        private sealed class ScriptedHttpMessageHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _steps;

            internal ScriptedHttpMessageHandler(
                params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] steps)
            {
                _steps = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(steps);
            }

            internal int CallCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                if (_steps.Count == 0)
                    throw new InvalidOperationException("The scripted handler received an unexpected request.");
                return _steps.Dequeue()(request, cancellationToken);
            }
        }

        private sealed class NonSeekableReadStream : MemoryStream
        {
            internal NonSeekableReadStream(byte[] buffer)
                : base(buffer, false)
            {
            }

            public override bool CanSeek => false;
        }

        private sealed class TlsTestServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly X509Certificate2 _certificate;
            private readonly Task _serverTask;

            internal TlsTestServer(X509Certificate2 certificate)
            {
                _certificate = certificate;
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                Address = new Uri("https://localhost:" + port + "/");
                _serverTask = Task.Run((Action)ServeRequests);
            }

            internal Uri Address { get; private set; }

            /// <inheritdoc />
            public void Dispose()
            {
                _listener.Stop();
                if (_serverTask.IsFaulted)
                    throw _serverTask.Exception.InnerException;
            }

            private void ServeRequests()
            {
                while (true)
                {
                    try
                    {
                        using (TcpClient client = _listener.AcceptTcpClient())
                        using (SslStream stream = new SslStream(client.GetStream(), false))
                        {
                            stream.ReadTimeout = 5000;
                            stream.WriteTimeout = 5000;
                            stream.AuthenticateAsServer(_certificate, false, SslProtocols.Tls12, false);
                            ReadRequestHeaders(stream);

                            byte[] response = Encoding.ASCII.GetBytes(
                                "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
                            stream.Write(response, 0, response.Length);
                            stream.Flush();
                        }
                    }
                    catch (AuthenticationException)
                    {
                        // Rejected client handshakes are expected in the negative TLS tests.
                    }
                    catch (IOException)
                    {
                        // The client may close the loopback stream immediately after a rejected handshake.
                    }
                    catch (SocketException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                }
            }

            private static void ReadRequestHeaders(Stream stream)
            {
                byte[] terminator = { 13, 10, 13, 10 };
                int matched = 0;
                while (matched < terminator.Length)
                {
                    int value = stream.ReadByte();
                    if (value < 0)
                        throw new IOException("The HTTP request ended before its headers were complete.");

                    matched = value == terminator[matched]
                        ? matched + 1
                        : value == terminator[0] ? 1 : 0;
                }
            }
        }
    }
}
