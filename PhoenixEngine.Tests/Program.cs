using PhoenixEngine.Request;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            Action[] tests =
            {
                CompletesValidTlsHandshakeAndConfiguresClientCertificates,
                RejectsExpiredCertificate,
                RejectsWrongHostCertificateWhenClientCertificatePathIsConfigured,
                RejectsUntrustedSelfSignedCertificate
            };

            int failures = 0;
            foreach (Action test in tests)
            {
                try
                {
                    test();
                    Console.WriteLine("Passed {0}", test.Method.Name);
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine("Failed {0}: {1}", test.Method.Name, exception);
                }
            }

            Console.WriteLine("{0} TLS tests passed; {1} failed.", tests.Length - failures, failures);
            return failures == 0 ? 0 : 1;
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
            HttpHelper helper = new HttpHelper();
            HttpItem item = new HttpItem
            {
                URL = "http://127.0.0.1/",
                ClentCertificates = new X509CertificateCollection { clientCertificate },
                CerPath = clientCertificatePath
            };

            MethodInfo setCertificate = typeof(HttpHelper).GetMethod(
                "SetCer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo requestField = typeof(HttpHelper).GetField(
                "Request",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (setCertificate == null || requestField == null)
                throw new InvalidOperationException("The HTTP certificate configuration seam was not found.");

            setCertificate.Invoke(helper, new object[] { item });
            HttpWebRequest request = (HttpWebRequest)requestField.GetValue(helper);
            AssertEqual(2, request.ClientCertificates.Count, "Both client certificate sources must be attached.");
            if (!ReferenceEquals(originalCallback, ServicePointManager.ServerCertificateValidationCallback))
                throw new InvalidOperationException("Client certificates changed the process-wide TLS policy.");
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
                _serverTask = Task.Run((Action)ServeSingleRequest);
            }

            internal Uri Address { get; private set; }

            /// <inheritdoc />
            public void Dispose()
            {
                _listener.Stop();
                if (!_serverTask.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The local TLS server did not stop.");
            }

            private void ServeSingleRequest()
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
                }
                catch (IOException)
                {
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
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
