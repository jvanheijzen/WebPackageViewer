using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows;
using WebPackageViewer.CommandLine;
using WebPackageViewer.Utilities;
using WebPackageViewer.Licensing;


namespace WebPackageViewer
{
    public partial class App : Application
    {
        public static string InitialStartDirectory { get; set; }

        public static string InitialUserStartedDirectory { get; set; }

        public static string TempUnpackDirectory { get; set; }

        public static WebPackageViewerCommandLine CommandLine { get; set; }
            = new WebPackageViewerCommandLine();

        public static bool IsConsoleApp { get; set; }


        protected override void OnStartup(StartupEventArgs e)
        {
            IsConsoleApp = AttachConsole(-1);


            if (IsConsoleApp)
            {
                // Delay slightly to let the existing prompt finish and then
                // start our output on a clean new line.
                System.Threading.Thread.Sleep(20);
                Console.WriteLine();
            }


            InitialStartDirectory = AppContext.BaseDirectory.TrimEnd('/');
            InitialUserStartedDirectory = Environment.CurrentDirectory;


            // Parse command-line arguments once.
            CommandLine.Parse();


            // Explicit GUI builder command:
            //
            //     WebPackageViewer.exe builder
            //
            // Keep this available even though the builder is also the
            // default interactive behavior when no Web site is present.
            if (CommandLine.ShowPackageBuilder)
            {
                ShowPackageBuilder(e);
                return;
            }


            // Package, unpackage, help, etc. are handled directly by the
            // command-line parser.
            if (!CommandLine.Unhandled)
            {
                if (IsConsoleApp)
                    ReleaseConsolePrompt();

                Environment.Exit(0);
            }


            var pack = new FilePackager();
            var exeFile = Assembly.GetExecutingAssembly().Location;


            // -------------------------------------------------------------
            // Packaged executable
            // -------------------------------------------------------------
            //
            // IMPORTANT:
            // Check for an appended Web package BEFORE deciding whether to
            // show the package-builder GUI.
            //
            // A packaged EXE normally has no command-line arguments, but it
            // must launch its embedded Web site rather than the builder.
            //
                        // Protected packages authorize the course before decrypting
            // the embedded Web site payload.
            var protectedPack = new ProtectedFilePackager();

            if (protectedPack.IsProtectedPackage(exeFile))
            {
                var manifest = protectedPack.ReadManifest(exeFile);

                if (manifest == null)
                {
                    MessageBox.Show(
                        "An error occurred reading the protected package.\n" +
                        protectedPack.ErrorMessage,
                        "Protected Web Package Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    Environment.Exit(1);
                }

                var requirement = manifest.ToLicenseRequirement();

                var licenseResult =
                    OfflineLicenseManager.ValidateInstalledLicense(
                        requirement);

                if (!licenseResult.IsValid)
                {
                    var activationWindow =
                        new LicenseActivationWindow(
                            requirement,
                            licenseResult.ErrorMessage);

                    if (activationWindow.ShowDialog() != true)
                        Environment.Exit(0);

                    licenseResult =
                        OfflineLicenseManager.ValidateInstalledLicense(
                            requirement);

                    if (!licenseResult.IsValid)
                    {
                        MessageBox.Show(
                            licenseResult.ErrorMessage,
                            "Offline License Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);

                        Environment.Exit(1);
                    }
                }

                var protectedOutputPath =
                    Path.Combine(
                        Path.GetTempPath(),
                        "dm_" + StringUtils.GenerateUniqueId(8));

                TempUnpackDirectory = protectedOutputPath;

                if (!protectedPack.UnpackageFile(
                    exeFile,
                    protectedOutputPath,
                    true))
                {
                    MessageBox.Show(
                        "An error occurred decrypting the viewer app and Web site.\n" +
                        protectedPack.ErrorMessage,
                        "Protected Web Package Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    Environment.Exit(1);
                }

                Environment.CurrentDirectory = protectedOutputPath;

                var protectedInnerExe =
                    Path.Combine(
                        protectedOutputPath,
                        "WebPackageViewer.exe");

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = protectedInnerExe,
                        WorkingDirectory = protectedOutputPath
                    });

                if (IsConsoleApp)
                    ReleaseConsolePrompt();

                Environment.Exit(0);
            }

            if (pack.FindMarkerOffset(exeFile, pack.SeparatorBytes) > 0)
            {
                var outputPath = Path.Combine(
                    Path.GetTempPath(),
                    "dm_" + StringUtils.GenerateUniqueId(8));

                TempUnpackDirectory = outputPath;


                if (!pack.UnpackageFile(exeFile, outputPath, true))
                {
                    MessageBox.Show(
                        "An error occurred unpacking the viewer app and Web site.\n" +
                        pack.ErrorMessage,
                        "Web Viewer Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    Environment.Exit(1);
                }


                Environment.CurrentDirectory = outputPath;

                var exe = Path.Combine(
                    outputPath,
                    "WebPackageViewer.exe");

                var p = Process.Start(
                    new ProcessStartInfo()
                    {
                        FileName = exe,
                        WorkingDirectory = outputPath
                    });


                Console.Write("\n✅ Launching Web Viewer...");


                if (IsConsoleApp)
                    ReleaseConsolePrompt();

                Environment.Exit(0);
            }


            // -------------------------------------------------------------
            // Default interactive behavior
            // -------------------------------------------------------------
            //
            // If the unpackaged WebPackageViewer is launched with no
            // arguments and there is no obvious Web site in the current
            // directory, show the graphical package builder.
            //
            // This makes double-clicking WebPackageViewer.exe useful to
            // normal Windows users while retaining all existing viewer and
            // command-line behaviors.
            //
            if (ShouldShowPackageBuilder(e))
            {
                ShowPackageBuilder(e);
                return;
            }


            if (IsConsoleApp)
                ReleaseConsolePrompt();


            // -------------------------------------------------------------
            // Normal Web viewer behavior
            // -------------------------------------------------------------

            // -------------------------------------------------------------
            // Offline licensing
            // -------------------------------------------------------------
            //
            // A package is licensed only when its extracted Web root contains
            // WebPackageViewer.license.json. Packages without that file continue
            // to behave exactly as they did before.
            //
            var licenseRequirement =
                OfflineLicenseManager.FindRequirement(
                    Environment.CurrentDirectory);

            if (licenseRequirement != null)
            {
                var licenseResult =
                    OfflineLicenseManager.ValidateInstalledLicense(
                        licenseRequirement);

                if (!licenseResult.IsValid)
                {
                    var activationWindow =
                        new LicenseActivationWindow(
                            licenseRequirement,
                            licenseResult.ErrorMessage);

                    if (activationWindow.ShowDialog() != true)
                    {
                        Shutdown();
                        return;
                    }

                    // Verify once more after the user imports a license.
                    licenseResult =
                        OfflineLicenseManager.ValidateInstalledLicense(
                            licenseRequirement);

                    if (!licenseResult.IsValid)
                    {
                        MessageBox.Show(
                            licenseResult.ErrorMessage,
                            "Offline License Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);

                        Shutdown();
                        return;
                    }
                }
            }

            // Read configuration from JSON and override with explicit values
            // passed on the command line.
            var config = WebViewerConfiguration.Read();


            if (!string.IsNullOrEmpty(CommandLine.VirtualPath))
                config.VirtualPath =
                    '/' + CommandLine.VirtualPath.Trim('/');
            else
                config.VirtualPath =
                    '/' + config.VirtualPath.Trim('/');


            if (!string.IsNullOrEmpty(CommandLine.InitialUrl))
                config.InitialUrl =
                    '/' + CommandLine.InitialUrl.Trim('/');


            if (string.IsNullOrEmpty(config.InitialUrl))
            {
                config.InitialUrl =
                    ('/' + config.VirtualPath + "/index.html")
                    .Replace("//", "/");
            }


            if (string.IsNullOrEmpty(config.WindowTitle))
                config.WindowTitle = "West Wind Web Package Viewer";


            // Override Web root from the command line.
            //
            // Example:
            //
            //     WebPackageViewer.exe "C:\MyWebSite"
            //
            if (e.Args.Length > 0 &&
                !e.Args[0].StartsWith("-"))
            {
                config.WebRootPath = e.Args[0];
            }


            base.OnStartup(e);


            // -------------------------------------------------------------
            // Ensure WebView2Loader.dll is available
            // -------------------------------------------------------------

            var exePath = Path.Combine(
                InitialStartDirectory,
                "WebView2Loader.dll");


            if (!File.Exists(exePath))
            {
                // If the loader is not present, we may be running from the
                // merged/single-file viewer and need to unpack the embedded
                // native loader first.
                try
                {
                    var loaderBytes =
                        ResourceHelper.LoadWebView2LoaderBytes();

                    File.WriteAllBytes(
                        "WebView2Loader.dll",
                        loaderBytes);
                }
                catch
                {
                    MessageBox.Show(
                        """
                        An error occurred unpacking the WebView2Loader.dll resource.

                        Make sure the application is not running from a read-only location and that you have permissions to write to the current directory.

                        Alternately manually copy `WebView2Loader.dll` from the same folder as the WebPackageViewer.exe to the current directory and restart the application.`
                        """,
                        "Web Viewer Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    Environment.Exit(1);
                }
            }


            MainWindow mainWindow =
                new MainWindow(config);

            mainWindow.Show();


            Current.Exit += (s, args) =>
            {
                if (!string.IsNullOrEmpty(
                    App.TempUnpackDirectory))
                {
                    // The viewer may still have files open briefly while
                    // shutting down, so remove the temporary package folder
                    // from a delayed background PowerShell process.
                    var exec =
                        $@"-ExecutionPolicy Bypass  -Command ""start-sleep -milliseconds 2000; remove-item '{App.TempUnpackDirectory}' -recurse -force"";Start-Sleep -Seconds 5";

                    var process =
                        Process.Start(
                            new ProcessStartInfo
                            {
                                FileName = "powershell",
                                Arguments = exec,
                                UseShellExecute = true,
                                WindowStyle =
                                    ProcessWindowStyle.Hidden,
                                WorkingDirectory =
                                    Path.GetTempPath()
                            });

                    process?.Dispose();
                }
            };
        }


        /// <summary>
        /// Determines whether a bare interactive launch should open the
        /// graphical package builder.
        ///
        /// Existing viewer behavior is preserved when:
        ///  - arguments were supplied;
        ///  - index.html exists in the current directory; or
        ///  - WebPackageViewer.config.json exists in the current directory.
        /// </summary>
        private static bool ShouldShowPackageBuilder(
            StartupEventArgs e)
        {
            if (e.Args.Length > 0)
                return false;


            var indexFile = Path.Combine(
                InitialUserStartedDirectory,
                "index.html");

            var configFile = Path.Combine(
                InitialUserStartedDirectory,
                "WebPackageViewer.config.json");


            return !File.Exists(indexFile) &&
                   !File.Exists(configFile);
        }


        /// <summary>
        /// Opens the graphical Web Package Builder.
        /// </summary>
        private void ShowPackageBuilder(
            StartupEventArgs e)
        {
            if (IsConsoleApp)
                ReleaseConsolePrompt();


            base.OnStartup(e);


            var builderWindow =
                new PackageBuilderWindow();

            builderWindow.Show();
        }


        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        static extern bool AllocConsole();


        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        static extern bool FreeConsole();


        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        static extern bool AttachConsole(
            int dwProcessId);


        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();


        [DllImport("user32.dll")]
        static extern bool ShowWindow(
            IntPtr h,
            int cmd);


        const byte VK_RETURN = 0x0D;


        [DllImport("user32.dll")]
        static extern void keybd_event(
            byte bVk,
            byte bScan,
            uint dwFlags,
            nuint dwExtraInfo);


        static void ReleaseConsolePrompt()
        {
            // Force another line break so the shell prompt appears cleanly.
            Console.WriteLine();

            FreeConsole();

            // Push Enter so PowerShell redraws its prompt.
            keybd_event(
                VK_RETURN,
                0,
                0,
                0);

            keybd_event(
                VK_RETURN,
                0,
                0x0002,
                0);
        }


        static bool StartedFromConsole()
        {
            if (AttachConsole(-1))
            {
                FreeConsole();
                return true;
            }

            // Already attached to a console also means console-launched.
            return Marshal.GetLastWin32Error() == 5;
        }
    }


    public static class ResourceHelper
    {
        /// <summary>
        /// Retrieve WebView2Loader.dll which cannot be embedded directly
        /// into the executable with ILRepack.
        /// </summary>
        /// <returns>The WebView2 loader DLL bytes.</returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the embedded WPF resource cannot be found.
        /// </exception>
        public static byte[] LoadWebView2LoaderBytes()
        {
            var asm =
                typeof(ResourceHelper).Assembly;


            using var resStream =
                asm.GetManifestResourceStream(
                    "WebPackageViewer.g.resources");


            if (resStream == null)
            {
                throw new FileNotFoundException(
                    "WebPackageViewer.g.resources not found.");
            }


            using var reader =
                new ResourceReader(resStream);


            foreach (DictionaryEntry entry in reader)
            {
                if ((string)entry.Key ==
                    "webview2loader.dll")
                {
                    using var stream =
                        (Stream)entry.Value;

                    using var ms =
                        new MemoryStream();

                    stream.CopyTo(ms);

                    return ms.ToArray();
                }
            }


            throw new FileNotFoundException(
                "WebView2Loader.dll was not found in WebPackageViewer.g.resources.");
        }
    }
}