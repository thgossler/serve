using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace serve
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            bool isElevated = false;
            bool useHttps = false;
            int exitTimeoutSecs = 300;
            string rootFolder = Directory.GetCurrentDirectory();
            int port = 8080; // Default port

            // Process command-line arguments
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                // Handle case-insensitive arguments with both "--" and "/" prefixes
                if (arg.StartsWith("--", true, CultureInfo.InvariantCulture) || arg.StartsWith("/", true, CultureInfo.InvariantCulture))
                {
                    string option = arg.Substring(arg.StartsWith("--") ? 2 : 1);

                    if (option.Equals("elevated", StringComparison.OrdinalIgnoreCase))
                    {
                        isElevated = true;
                    }
                    else if (option.Equals("https", StringComparison.OrdinalIgnoreCase))
                    {
                        useHttps = true;
                        Console.WriteLine("Using HTTPS");
                    }
                    else if (option.StartsWith("port:", StringComparison.OrdinalIgnoreCase))
                    {
                        string portValue = option.Substring(5);
                        if (int.TryParse(portValue, out int parsedPort))
                        {
                            port = parsedPort;
                            Console.WriteLine($"Using port {port}");
                        }
                        else
                        {
                            Console.WriteLine($"Invalid port number: {portValue}");
                            return (int)ExitCode.InvalidPort;
                        }
                    }
                    else if (option.StartsWith("exitTimeoutSecs:", StringComparison.OrdinalIgnoreCase))
                    {
                        string exitTimeoutSecsValue = option.Substring(16);
                        if (int.TryParse(exitTimeoutSecsValue, out int parsedExitTimeoutSecs))
                        {
                            exitTimeoutSecs = parsedExitTimeoutSecs;
                            Console.WriteLine($"Exiting after {exitTimeoutSecs} seconds inactivity");
                        }
                        else
                        {
                            Console.WriteLine($"Invalid exit timeout seconds value: {exitTimeoutSecsValue}");
                            return (int)ExitCode.InvalidExitTimeoutSecsValue;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unknown option: {arg}");
                        return (int)ExitCode.UnknownOption;
                    }
                }
                else
                {
                    // Assume it's the root folder path
                    rootFolder = arg;
                }
            }

            if (!Directory.Exists(rootFolder))
            {
                Console.WriteLine($"Error: Directory '{rootFolder}' does not exist.");
                return (int)ExitCode.RootFolderDoesNotExist;
            }
            else
            {
                Console.WriteLine($"Serving files from '{rootFolder}'");
            }

            // Certificate parameters
            string certPassword = "password"; // Use a secure password in production
            string certName = "localhost";
            string friendlyName = "serve tool certificate";
            string certPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "serve", "localhost.pfx");

            X509Certificate2 certificate = null;
            if (useHttps)
            {
                // Check if the certificate exists in the certificate store
                certificate = CertificateHelper.FindCertificateInStore(friendlyName);

                if (certificate == null)
                {
                    // Certificate not found in the store
                    // Delete the certificate file on disk if it exists
                    if (File.Exists(certPath))
                    {
                        try
                        {
                            File.Delete(certPath);
                            Console.WriteLine("Deleted existing certificate file on disk.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to delete certificate file on disk: {ex.Message}");
                            return (int)ExitCode.FailedToDeleteCertificateFile;
                        }
                    }

                    // Check if running as administrator
                    if (!IsAdministrator())
                    {
                        Console.WriteLine("Administrator privileges are required to install the certificate...");

                        // Relaunch the application with elevated privileges
                        var psi = new ProcessStartInfo
                        {
                            FileName = Process.GetCurrentProcess().MainModule.FileName,
                            Arguments = $"--elevated \"{rootFolder}\" --port={port}",
                            Verb = "runas",
                            UseShellExecute = true
                        };

                        try
                        {
                            var proc = Process.Start(psi);
                            proc.WaitForExit();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Failed to obtain administrator privileges: " + ex.Message);
                            return (int)ExitCode.FailedToObtainAdminPrivileges;
                        }

                        // After elevation, check if the certificate was installed
                        certificate = CertificateHelper.FindCertificateInStore(friendlyName);
                        if (certificate == null)
                        {
                            Console.WriteLine("Certificate installation failed or was canceled.");
                            return (int)ExitCode.CertificateInstallationFailed;
                        }
                    }
                    else if (isElevated)
                    {
                        // Generate and install certificate
                        Directory.CreateDirectory(Path.GetDirectoryName(certPath));

                        certificate = CertificateHelper.GenerateSelfSignedCertificate(certName, certPassword, friendlyName);

                        byte[] pfxBytes = certificate.Export(X509ContentType.Pfx, certPassword);
                        File.WriteAllBytes(certPath, pfxBytes);

                        // Install the certificate
                        CertificateHelper.InstallCertificate(certificate);

                        // Exit elevated process after installing certificate
                        return (int)ExitCode.Success;
                    }
                }
                else
                {
                    Console.WriteLine("Certificate found in the certificate store");
                }

                // Load the certificate
                if (certificate == null && File.Exists(certPath))
                {
                    // Fallback: Load certificate from disk if available
                    certificate = new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                }

                if (certificate == null)
                {
                    Console.WriteLine("Failed to load certificate");
                    return (int)ExitCode.FailedToLoadCertificate;
                }
            }

            // Build the host
            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(port, listenOptions =>
                {
                    if (useHttps)
                    {
                        listenOptions.UseHttps(certificate);
                    }
                });
            });

            var app = builder.Build();

            var fileProvider = new PhysicalFileProvider(rootFolder);

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = ""
            });

            // Middleware to handle 404 errors and attempt to serve index.html from the request path
            app.Use(async (context, next) =>
            {
                await next();

                if (context.Response.StatusCode == 404)
                {
                    // Attempt to serve index.html appended to the original request path
                    var originalPath = context.Request.Path;
                    var indexPath = originalPath.Add("/index.html");

                    var fileInfo = fileProvider.GetFileInfo(indexPath.Value);

                    if (fileInfo.Exists && !fileInfo.IsDirectory)
                    {
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "text/html";
                        await context.Response.SendFileAsync(fileInfo);
                    }
                }
            });

            // Middleware to track last request time
            DateTime lastRequestTime = DateTime.Now;

            app.Use(async (context, next) =>
            {
                lastRequestTime = DateTime.Now;
                await next();
            });

            // Start the web host
            var webHostTask = app.RunAsync();

            // Open the default web browser
            try
            {
                var protocol = useHttps ? "https" : "http";
                Process.Start(new ProcessStartInfo($"{protocol}://localhost:{port}/") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to open web browser: " + ex.Message);
            }

            // Background task to monitor inactivity
            var cts = new CancellationTokenSource();
            var inactivityTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    if ((DateTime.Now - lastRequestTime).TotalSeconds >= exitTimeoutSecs)
                    {
                        Console.WriteLine($"No requests received for {exitTimeoutSecs} seconds. Shutting down.");
                        cts.Cancel();
                        await app.StopAsync();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
                }
            });

            await Task.WhenAny(webHostTask, inactivityTask);

            return (int)ExitCode.Success;
        }

        static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public enum ExitCode : int
    {
        Success = 0,
        UnknownOption,
        InvalidPort,
        RootFolderDoesNotExist,
        FailedToDeleteCertificateFile,
        FailedToObtainAdminPrivileges,
        CertificateInstallationFailed,
        FailedToLoadCertificate,
        InvalidExitTimeoutSecsValue
    }

    public static class CertificateHelper
    {
        public static X509Certificate2 GenerateSelfSignedCertificate(string certName, string certPassword, string friendlyName)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={certName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Subject Alternative Name
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(certName);
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Basic Constraints - mark as not CA
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            // Key Usage
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

            // Enhanced Key Usage
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                false));

            var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(10));

            // Set the Friendly Name
            certificate.FriendlyName = friendlyName;

            // Export PFX with private key
            var pfxBytes = certificate.Export(X509ContentType.Pfx, certPassword);
            return new X509Certificate2(pfxBytes, certPassword, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        }

        public static void InstallCertificate(X509Certificate2 certificate)
        {
            try
            {
                // Install into LocalMachine Trusted Root Certification Authorities store
                using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
                store.Close();
                Console.WriteLine("Certificate installed into LocalMachine Trusted Root Certification Authorities store.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to install certificate into LocalMachine Trusted Root Certification Authorities store. " + ex.Message);
            }
        }

        public static X509Certificate2 FindCertificateInStore(string friendlyName)
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            foreach (var cert in store.Certificates)
            {
                if (cert.FriendlyName.Equals(friendlyName, StringComparison.OrdinalIgnoreCase))
                {
                    store.Close();
                    return cert;
                }
            }

            store.Close();
            return null;
        }
    }
}
