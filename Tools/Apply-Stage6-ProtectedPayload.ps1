$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

$appPath = Join-Path $repoRoot "WebPackageViewer\App.xaml.cs"
$builderPath = Join-Path $repoRoot "WebPackageViewer\PackageBuilderWindow.xaml.cs"

if (-not (Test-Path $appPath)) {
    throw "Could not find $appPath"
}

if (-not (Test-Path $builderPath)) {
    throw "Could not find $builderPath"
}

$app = [System.IO.File]::ReadAllText($appPath)

if ($app -notmatch "ProtectedFilePackager") {
    $needle = 'if (pack.FindMarkerOffset(exeFile, pack.SeparatorBytes) > 0)'

    if (-not $app.Contains($needle)) {
        throw "Could not locate the legacy package-detection block in App.xaml.cs."
    }

    $protectedBlock = @'
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


'@

    $app = $app.Replace(
        $needle,
        $protectedBlock + "            " + $needle)

    [System.IO.File]::WriteAllText(
        $appPath,
        $app,
        [System.Text.UTF8Encoding]::new($true))

    Write-Host "Patched App.xaml.cs for protected package startup." -ForegroundColor Green
}
else {
    Write-Host "App.xaml.cs already contains protected package support."
}

$builder = [System.IO.File]::ReadAllText($builderPath)

if ($builder -notmatch "protectedPackager\.PackageFile") {
    $old = @'
                var packageExe = Assembly.GetExecutingAssembly().Location;

                if (!packager.PackageFile(
                    outputFile,
                    packageExe,
                    generatedZip))
                {
                    return BuildResult.Fail(
                        "Failed to create the packaged executable.\n\n" +
                        packager.ErrorMessage);
                }
'@

    if (-not $builder.Contains($old)) {
        throw "Could not locate the packaging block in PackageBuilderWindow.xaml.cs."
    }

    $new = @'
                var packageExe = Assembly.GetExecutingAssembly().Location;

                if (requireOfflineLicense)
                {
                    var protectedRequirement =
                        new OfflineLicenseRequirement
                        {
                            Version = 1,
                            CourseId = course.ProductCode,
                            CourseName = course.CourseName,
                            CourseVersion = course.CourseVersion,
                            ModuleId = moduleId,
                            ModuleName = moduleName
                        };

                    var protectedPackager =
                        new ProtectedFilePackager();

                    if (!protectedPackager.PackageFile(
                        outputFile,
                        packageExe,
                        generatedZip,
                        protectedRequirement))
                    {
                        return BuildResult.Fail(
                            "Failed to create the protected packaged executable.\n\n" +
                            protectedPackager.ErrorMessage);
                    }
                }
                else
                {
                    if (!packager.PackageFile(
                        outputFile,
                        packageExe,
                        generatedZip))
                    {
                        return BuildResult.Fail(
                            "Failed to create the packaged executable.\n\n" +
                            packager.ErrorMessage);
                    }
                }
'@

    $builder = $builder.Replace($old, $new)

    [System.IO.File]::WriteAllText(
        $builderPath,
        $builder,
        [System.Text.UTF8Encoding]::new($true))

    Write-Host "Patched PackageBuilderWindow.xaml.cs for protected payload builds." -ForegroundColor Green
}
else {
    Write-Host "PackageBuilderWindow.xaml.cs already contains protected payload support."
}

Write-Host ""
Write-Host "Stage 6 source patch complete." -ForegroundColor Green
Write-Host "Next: dotnet build .\WebPackageViewer.slnx -c Release"
