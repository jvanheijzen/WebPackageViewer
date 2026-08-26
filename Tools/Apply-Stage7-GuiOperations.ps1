$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Write-Utf8BomFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Text
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Text,
        [System.Text.UTF8Encoding]::new($true))
}

# ------------------------------------------------------------
# Viewer project: System.Windows.Forms is used only for the
# native folder chooser in the Batch Builder.
# ------------------------------------------------------------
$viewerProject =
    Join-Path $repoRoot "WebPackageViewer\WebPackageViewer.csproj"

$projectText =
    [System.IO.File]::ReadAllText($viewerProject)

if ($projectText -notmatch '<Reference Include="System.Windows.Forms"') {
    $projectClose = '</Project>'

    if (-not $projectText.Contains($projectClose)) {
        throw "Could not locate </Project> in WebPackageViewer.csproj."
    }

    $referenceBlock = @'
  <ItemGroup>
    <Reference Include="System.Windows.Forms" />
  </ItemGroup>

</Project>
'@

    $projectText =
        $projectText.Replace(
            $projectClose,
            $referenceBlock)

    Write-Utf8BomFile `
        -Path $viewerProject `
        -Text $projectText

    Write-Host "Added System.Windows.Forms reference." -ForegroundColor Green
}
else {
    Write-Host "System.Windows.Forms reference already present."
}

# ------------------------------------------------------------
# Package Builder: replace the single Build button area with a
# Batch Build + Build Package button group.
# ------------------------------------------------------------
$builderXaml =
    Join-Path $repoRoot "WebPackageViewer\PackageBuilderWindow.xaml"

$builderXamlText =
    [System.IO.File]::ReadAllText($builderXaml)

if ($builderXamlText -notmatch 'Content="Batch Build\.\.\."') {
    $oldBuildButton = @'
                        <Button Grid.Column="1"
                                x:Name="BuildButton"
                                Width="140"
                                Height="38"
                                FontWeight="SemiBold"
                                Content="Build Package"
                                Click="BuildButton_Click"/>
'@

    if (-not $builderXamlText.Contains($oldBuildButton)) {
        throw "Could not locate the Build Package button in PackageBuilderWindow.xaml."
    }

    $newBuildButtons = @'
                        <StackPanel Grid.Column="1"
                                    Orientation="Horizontal">
                            <Button Width="125"
                                    Height="38"
                                    Margin="0,0,8,0"
                                    Content="Batch Build..."
                                    Click="BatchBuildButton_Click"/>
                            <Button x:Name="BuildButton"
                                    Width="140"
                                    Height="38"
                                    FontWeight="SemiBold"
                                    Content="Build Package"
                                    Click="BuildButton_Click"/>
                        </StackPanel>
'@

    $builderXamlText =
        $builderXamlText.Replace(
            $oldBuildButton,
            $newBuildButtons)

    Write-Utf8BomFile `
        -Path $builderXaml `
        -Text $builderXamlText

    Write-Host "Added Batch Build button to Package Builder." -ForegroundColor Green
}
else {
    Write-Host "Package Builder already contains Batch Build button."
}

# Add BatchBuildButton_Click to code-behind.
$builderCode =
    Join-Path $repoRoot "WebPackageViewer\PackageBuilderWindow.xaml.cs"

$builderCodeText =
    [System.IO.File]::ReadAllText($builderCode)

if ($builderCodeText -notmatch 'BatchBuildButton_Click') {
    $insertBefore =
        '        private class BuildResult'

    if (-not $builderCodeText.Contains($insertBefore)) {
        throw "Could not find BuildResult insertion point in PackageBuilderWindow.xaml.cs."
    }

    $batchHandler = @'
        private void BatchBuildButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var window =
                new BatchPackageBuilderWindow
                {
                    Owner = this
                };

            window.ShowDialog();
        }

'@

    $builderCodeText =
        $builderCodeText.Replace(
            $insertBefore,
            $batchHandler + $insertBefore)

    Write-Utf8BomFile `
        -Path $builderCode `
        -Text $builderCodeText

    Write-Host "Added Batch Build handler." -ForegroundColor Green
}
else {
    Write-Host "Package Builder already contains Batch Build handler."
}

# ------------------------------------------------------------
# License Generator UI:
# - Installed Licenses under course management
# - Operator Setup under signing-key controls
# ------------------------------------------------------------
$generatorXaml =
    Join-Path $repoRoot "WebPackageLicenseGenerator\MainWindow.xaml"

$generatorXamlText =
    [System.IO.File]::ReadAllText($generatorXaml)

if ($generatorXamlText -notmatch 'Content="Installed Licenses\.\.\."') {
    $oldManageButton = @'
   <Button Grid.Column="1"
           Width="125"
           Margin="10,21,0,0"
           Content="Manage Courses..."
           Click="ManageCoursesButton_Click"/>
'@

    if (-not $generatorXamlText.Contains($oldManageButton)) {
        throw "Could not locate Manage Courses button in License Generator MainWindow.xaml."
    }

    $newManageButtons = @'
   <StackPanel Grid.Column="1"
               Margin="10,21,0,0">
    <Button Width="145"
            Height="30"
            Margin="0,0,0,6"
            Content="Manage Courses..."
            Click="ManageCoursesButton_Click"/>
    <Button Width="145"
            Height="30"
            Content="Installed Licenses..."
            Click="InstalledLicensesButton_Click"/>
   </StackPanel>
'@

    $generatorXamlText =
        $generatorXamlText.Replace(
            $oldManageButton,
            $newManageButtons)

    Write-Host "Added Installed Licenses button." -ForegroundColor Green
}

if ($generatorXamlText -notmatch 'Content="Operator Setup\.\.\."') {
    $oldSigningButtons = @'
    <StackPanel Grid.Column="1"
                Orientation="Horizontal"
                Margin="12,0,0,0">
     <Button Width="155"
             Height="30"
             Margin="0,0,8,0"
             Content="Export Recovery Backup..."
             Click="ExportRecoveryBackupButton_Click"/>
     <Button Width="155"
             Height="30"
             Content="Restore Recovery Backup..."
             Click="RestoreRecoveryBackupButton_Click"/>
    </StackPanel>
'@

    if (-not $generatorXamlText.Contains($oldSigningButtons)) {
        throw "Could not locate signing-key button group in License Generator MainWindow.xaml."
    }

    $newSigningButtons = @'
    <StackPanel Grid.Column="1"
                Margin="12,0,0,0">
     <StackPanel Orientation="Horizontal">
      <Button Width="155"
              Height="30"
              Margin="0,0,8,0"
              Content="Export Recovery Backup..."
              Click="ExportRecoveryBackupButton_Click"/>
      <Button Width="155"
              Height="30"
              Content="Restore Recovery Backup..."
              Click="RestoreRecoveryBackupButton_Click"/>
     </StackPanel>
     <Button Width="318"
             Height="30"
             Margin="0,6,0,0"
             Content="Operator Setup..."
             Click="OperatorSetupButton_Click"/>
    </StackPanel>
'@

    $generatorXamlText =
        $generatorXamlText.Replace(
            $oldSigningButtons,
            $newSigningButtons)

    Write-Host "Added Operator Setup button." -ForegroundColor Green
}

# Give the extra controls a little more room.
$generatorXamlText =
    $generatorXamlText.Replace(
        'Width="760" Height="660"',
        'Width="800" Height="700"')

Write-Utf8BomFile `
    -Path $generatorXaml `
    -Text $generatorXamlText

# Generator code-behind handlers and signing identity safety check.
$generatorCode =
    Join-Path $repoRoot "WebPackageLicenseGenerator\MainWindow.xaml.cs"

$generatorCodeText =
    [System.IO.File]::ReadAllText($generatorCode)

if ($generatorCodeText -notmatch 'InstalledLicensesButton_Click') {
    $insertBefore =
        '        private void CourseComboBox_SelectionChanged'

    if (-not $generatorCodeText.Contains($insertBefore)) {
        throw "Could not find CourseComboBox_SelectionChanged insertion point in MainWindow.xaml.cs."
    }

    $handlers = @'
        private void InstalledLicensesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var window =
                new InstalledLicensesWindow
                {
                    Owner = this
                };

            window.ShowDialog();
        }

        private void OperatorSetupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var current =
                CourseComboBox.SelectedItem
                as CourseDefinition;

            var window =
                new OperatorSetupWindow
                {
                    Owner = this
                };

            window.ShowDialog();

            LoadCourses(current?.ProductCode);
            RefreshKeyStatus();
        }

'@

    $generatorCodeText =
        $generatorCodeText.Replace(
            $insertBefore,
            $handlers + $insertBefore)

    Write-Host "Added License Generator GUI handlers." -ForegroundColor Green
}

# Upgrade the key status so a restored-but-wrong identity is obvious.
$oldRefreshKeyStatus = @'
        private void RefreshKeyStatus()
        {
            KeyStatusTextBlock.Text =
                SigningKeyStore.HasPrivateKey
                    ? "Signing key initialized. Create a portable recovery backup before moving to another computer or issuing production licenses."
                    : "Signing key not found. Restore the existing signing identity before generating licenses.";
        }
'@

$newRefreshKeyStatus = @'
        private void RefreshKeyStatus()
        {
            if (!SigningKeyStore.HasPrivateKey)
            {
                KeyStatusTextBlock.Text =
                    "Signing key not found. Restore the existing signing identity before generating licenses.";
                return;
            }

            string verificationError;

            KeyStatusTextBlock.Text =
                SigningIdentityVerifier.MatchesViewerPublicKey(
                    out verificationError)
                    ? "Signing key ready. The installed signing identity matches this WebPackageViewer build."
                    : "WARNING: " + verificationError;
        }
'@

if ($generatorCodeText.Contains($oldRefreshKeyStatus)) {
    $generatorCodeText =
        $generatorCodeText.Replace(
            $oldRefreshKeyStatus,
            $newRefreshKeyStatus)

    Write-Host "Added signing-identity verification status." -ForegroundColor Green
}

# Prevent production licenses from being signed by a key that the viewer
# cannot verify.
$oldSigningKeyCheck = @'
                if (!SigningKeyStore.HasPrivateKey)
                {
                    MessageBox.Show(
                        this,
                        "The signing key is not available. Restore the existing signing identity first.",
                        "License Generator",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
'@

$newSigningKeyCheck = @'
                if (!SigningKeyStore.HasPrivateKey)
                {
                    MessageBox.Show(
                        this,
                        "The signing key is not available. Restore the existing signing identity first.",
                        "License Generator",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string verificationError;

                if (!SigningIdentityVerifier.MatchesViewerPublicKey(
                    out verificationError))
                {
                    MessageBox.Show(
                        this,
                        verificationError +
                        "\n\nUse Operator Setup to restore the correct signing identity before generating production licenses.",
                        "Signing Identity Mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
'@

if ($generatorCodeText.Contains($oldSigningKeyCheck) -and
    $generatorCodeText -notmatch 'Signing Identity Mismatch') {
    $generatorCodeText =
        $generatorCodeText.Replace(
            $oldSigningKeyCheck,
            $newSigningKeyCheck)

    Write-Host "Added production license signing guard." -ForegroundColor Green
}

Write-Utf8BomFile `
    -Path $generatorCode `
    -Text $generatorCodeText

Write-Host ""
Write-Host "Stage 7 GUI operations patch complete." -ForegroundColor Green
Write-Host "Build the solution in Release from Visual Studio."
