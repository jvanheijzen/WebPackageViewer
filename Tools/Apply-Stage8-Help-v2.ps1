$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Read-Text {
    param([Parameter(Mandatory)][string]$Path)
    # Normalize Windows CRLF and legacy CR line endings to LF for matching.
    # The original Stage 8 patcher accidentally performed a no-op here,
    # which caused exact source-block matches to fail on normal Windows files.
    return [System.IO.File]::ReadAllText($Path).Replace("`r`n", "`n").Replace("`r", "`n")
}

function Write-Text {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Text
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Text,
        [System.Text.UTF8Encoding]::new($true))
}

function Replace-Exact {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Old,
        [Parameter(Mandatory)][string]$New,
        [Parameter(Mandatory)][string]$AlreadyMarker,
        [Parameter(Mandatory)][string]$Description
    )

    $text = Read-Text $Path

    if ($text.Contains($AlreadyMarker)) {
        Write-Host "$Description already applied."
        return
    }

    if (-not $text.Contains($Old)) {
        throw "Could not locate the expected source block for: $Description`nFile: $Path"
    }

    $text = $text.Replace($Old, $New)
    Write-Text -Path $Path -Text $text
    Write-Host "$Description" -ForegroundColor Green
}

function Add-Using {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$After,
        [Parameter(Mandatory)][string]$Using
    )

    $text = Read-Text $Path

    if ($text.Contains($Using)) {
        return
    }

    if (-not $text.Contains($After)) {
        throw "Could not add using directive to $Path"
    }

    $text = $text.Replace(
        $After,
        $After + [Environment]::NewLine + $Using)

    Write-Text -Path $Path -Text $text
}

# ------------------------------------------------------------------
# Embed only the distributor-safe manual into WebPackageViewer.exe.
# The administrator manual intentionally remains external and MSI-only.
# ------------------------------------------------------------------
$viewerProject =
    Join-Path $repoRoot "WebPackageViewer\WebPackageViewer.csproj"

$projectText = Read-Text $viewerProject

if ($projectText -notmatch 'WebPackageViewer\.DistributorHelp\.html') {
    $insert = @'
  <ItemGroup>
    <EmbeddedResource Include="..\Documentation\WebPackageCourses-Distributor.html">
      <LogicalName>WebPackageViewer.DistributorHelp.html</LogicalName>
    </EmbeddedResource>
  </ItemGroup>

</Project>
'@

    if (-not $projectText.Contains('</Project>')) {
        throw "Could not locate </Project> in WebPackageViewer.csproj."
    }

    $projectText =
        $projectText.Replace(
            '</Project>',
            $insert)

    Write-Text -Path $viewerProject -Text $projectText
    Write-Host "Embedded distributor help resource in WebPackageViewer." -ForegroundColor Green
}
else {
    Write-Host "Distributor help resource already configured."
}

# ------------------------------------------------------------------
# Web Package Builder: F1 + visible Help button.
# ------------------------------------------------------------------
$path = Join-Path $repoRoot "WebPackageViewer\PackageBuilderWindow.xaml"
$old = @'
                            <Button Width="125"
                                    Height="38"
                                    Margin="0,0,8,0"
                                    Content="Batch Build..."
                                    Click="BatchBuildButton_Click"/>
'@
$new = @'
                            <Button Width="80"
                                    Height="38"
                                    Margin="0,0,8,0"
                                    Content="Help"
                                    Click="HelpButton_Click"/>
                            <Button Width="125"
                                    Height="38"
                                    Margin="0,0,8,0"
                                    Content="Batch Build..."
                                    Click="BatchBuildButton_Click"/>
'@
Replace-Exact -Path $path -Old $old -New $new `
    -AlreadyMarker 'Click="HelpButton_Click"' `
    -Description "Added Help button to Web Package Builder."

$path = Join-Path $repoRoot "WebPackageViewer\PackageBuilderWindow.xaml.cs"
Add-Using -Path $path `
    -After 'using WebPackageViewer.Licensing;' `
    -Using 'using WebPackageViewer.Help;'

$text = Read-Text $path
if ($text -notmatch 'AttachAdministratorHelp\(this, "builder-single"\)') {
    $old = @'
            InitializeComponent();
            ReloadCourses();
'@
    $new = @'
            InitializeComponent();
            ReloadCourses();
            HelpLauncher.AttachAdministratorHelp(this, "builder-single");
'@
    if (-not $text.Contains($old)) { throw "Could not patch PackageBuilderWindow constructor." }
    $text = $text.Replace($old,$new)
}
if ($text -notmatch 'private void HelpButton_Click') {
    $marker = '        private void BatchBuildButton_Click('
    $method = @'
        private void HelpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HelpLauncher.ShowAdministratorHelp(
                this,
                "builder-single");
        }

'@
    if (-not $text.Contains($marker)) { throw "Could not add builder HelpButton_Click." }
    $text = $text.Replace($marker,$method+$marker)
}
Write-Text -Path $path -Text $text

# ------------------------------------------------------------------
# Batch Builder: F1 + visible Help button.
# ------------------------------------------------------------------
$path = Join-Path $repoRoot "WebPackageViewer\BatchPackageBuilderWindow.xaml"
$old = @'
                <Button Width="90"
                        Content="Clear"
                        Click="ClearButton_Click"/>
'@
$new = @'
                <Button Width="90"
                        Margin="0,0,8,0"
                        Content="Clear"
                        Click="ClearButton_Click"/>
                <Button Width="80"
                        Content="Help"
                        Click="HelpButton_Click"/>
'@
Replace-Exact -Path $path -Old $old -New $new `
    -AlreadyMarker 'Click="HelpButton_Click"' `
    -Description "Added Help button to Batch Package Builder."

$path = Join-Path $repoRoot "WebPackageViewer\BatchPackageBuilderWindow.xaml.cs"
Add-Using -Path $path `
    -After 'using WebPackageViewer.Licensing;' `
    -Using 'using WebPackageViewer.Help;'
$text = Read-Text $path
if ($text -notmatch 'AttachAdministratorHelp\(this, "builder-batch"\)') {
    $old = @'
            InitializeComponent();
            DataContext = this;
'@
    $new = @'
            InitializeComponent();
            DataContext = this;
            HelpLauncher.AttachAdministratorHelp(this, "builder-batch");
'@
    if (-not $text.Contains($old)) { throw "Could not patch BatchPackageBuilderWindow constructor." }
    $text = $text.Replace($old,$new)
}
if ($text -notmatch 'private void HelpButton_Click') {
    $marker = '        private async void BuildAllButton_Click('
    $method = @'
        private void HelpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HelpLauncher.ShowAdministratorHelp(
                this,
                "builder-batch");
        }

'@
    if (-not $text.Contains($marker)) { throw "Could not add Batch Builder HelpButton_Click." }
    $text = $text.Replace($marker,$method+$marker)
}
Write-Text -Path $path -Text $text

# ------------------------------------------------------------------
# Add Course: F1.
# ------------------------------------------------------------------
$path = Join-Path $repoRoot "WebPackageViewer\AddCourseWindow.xaml.cs"
Add-Using -Path $path `
    -After 'using WebPackageViewer.CourseCatalog;' `
    -Using 'using WebPackageViewer.Help;'
$text = Read-Text $path
if ($text -notmatch 'AttachAdministratorHelp\(this, "course-catalog"\)') {
    $old = @'
        public AddCourseWindow()
        {
            InitializeComponent();
        }
'@
    $new = @'
        public AddCourseWindow()
        {
            InitializeComponent();
            HelpLauncher.AttachAdministratorHelp(this, "course-catalog");
        }
'@
    if (-not $text.Contains($old)) { throw "Could not patch AddCourseWindow constructor." }
    $text = $text.Replace($old,$new)
    Write-Text -Path $path -Text $text
}

# ------------------------------------------------------------------
# Distributor activation: F1 + visible Help button.
# ------------------------------------------------------------------
$path = Join-Path $repoRoot "WebPackageViewer\LicenseActivationWindow.xaml"
$old = @'
            <Button Width="120" Margin="0,0,10,0"
                    Content="Close" Click="CloseButton_Click"/>
'@
$new = @'
            <Button Width="80" Margin="0,0,10,0"
                    Content="Help" Click="HelpButton_Click"/>
            <Button Width="120" Margin="0,0,10,0"
                    Content="Close" Click="CloseButton_Click"/>
'@
Replace-Exact -Path $path -Old $old -New $new `
    -AlreadyMarker 'Content="Help" Click="HelpButton_Click"' `
    -Description "Added Help button to Offline License Required."

$path = Join-Path $repoRoot "WebPackageViewer\LicenseActivationWindow.xaml.cs"
Add-Using -Path $path `
    -After 'using WebPackageViewer.Licensing;' `
    -Using 'using WebPackageViewer.Help;'
$text = Read-Text $path
if ($text -notmatch 'AttachDistributorHelp\(this, "dist-activate"\)') {
    $old = '            InitializeComponent();'
    $new = "            InitializeComponent();`n            HelpLauncher.AttachDistributorHelp(this, `"dist-activate`");"
    if (-not $text.Contains($old)) { throw "Could not patch LicenseActivationWindow constructor." }
    $text = $text.Replace($old,$new)
}
if ($text -notmatch 'private void HelpButton_Click') {
    $marker = '        private void CopyMachineIdButton_Click'
    $method = @'
        private void HelpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HelpLauncher.ShowDistributorHelp(
                this,
                "dist-activate");
        }

'@
    if (-not $text.Contains($marker)) { throw "Could not add activation HelpButton_Click." }
    $text = $text.Replace($marker,$method+$marker)
}
Write-Text -Path $path -Text $text

# ------------------------------------------------------------------
# Course viewer: F1 + ? title-bar button.
# ------------------------------------------------------------------
$path = Join-Path $repoRoot "WebPackageViewer\MainWindow.xaml"
$text = Read-Text $path
if ($text -notmatch 'Click="HelpButton_Click"') {
    $oldCols = @'
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="46"/>
                    <ColumnDefinition Width="46"/>
                    <ColumnDefinition Width="46"/>
'@
    $newCols = @'
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="46"/>
                    <ColumnDefinition Width="46"/>
                    <ColumnDefinition Width="46"/>
                    <ColumnDefinition Width="46"/>
'@
    if (-not $text.Contains($oldCols)) { throw "Could not patch MainWindow title-bar columns." }
    $text = $text.Replace($oldCols,$newCols)

    $oldMin = @'
                <Button Grid.Column="1"
                        Style="{DynamicResource TitleBarButtonStyle}"
                        FontFamily="Segoe MDL2 Assets"
                        FontSize="12"
                        shell:WindowChrome.IsHitTestVisibleInChrome="True"
                        Content="&#xE921;"
                        Click="MinimizeButton_Click"/>
'@
    $newMin = @'
                <Button Grid.Column="1"
                        Style="{DynamicResource TitleBarButtonStyle}"
                        FontSize="15"
                        FontWeight="SemiBold"
                        shell:WindowChrome.IsHitTestVisibleInChrome="True"
                        Content="?"
                        ToolTip="Help (F1)"
                        Click="HelpButton_Click"/>

                <Button Grid.Column="2"
                        Style="{DynamicResource TitleBarButtonStyle}"
                        FontFamily="Segoe MDL2 Assets"
                        FontSize="12"
                        shell:WindowChrome.IsHitTestVisibleInChrome="True"
                        Content="&#xE921;"
                        Click="MinimizeButton_Click"/>
'@
    if (-not $text.Contains($oldMin)) { throw "Could not patch MainWindow minimize button." }
    $text = $text.Replace($oldMin,$newMin)
    $text = $text.Replace('<Button x:Name="MaximizeRestoreButton"`n                        Grid.Column="2"','<Button x:Name="MaximizeRestoreButton"`n                        Grid.Column="3"')
    $text = $text.Replace('<Button Grid.Column="3"`n                        Style="{DynamicResource TitleBarButtonStyle}"','<Button Grid.Column="4"`n                        Style="{DynamicResource TitleBarButtonStyle}"')
    # Handle LF-only files too.
    $text = $text.Replace("<Button x:Name=`"MaximizeRestoreButton`"`n                        Grid.Column=`"2`"","<Button x:Name=`"MaximizeRestoreButton`"`n                        Grid.Column=`"3`"")
    $text = $text.Replace("<Button Grid.Column=`"3`"`n                        Style=`"{DynamicResource TitleBarButtonStyle}`"","<Button Grid.Column=`"4`"`n                        Style=`"{DynamicResource TitleBarButtonStyle}`"")
    Write-Text -Path $path -Text $text
    Write-Host "Added distributor Help button to course viewer title bar." -ForegroundColor Green
}

$path = Join-Path $repoRoot "WebPackageViewer\MainWindow.xaml.cs"
Add-Using -Path $path `
    -After 'using System.Windows.Interop;' `
    -Using 'using WebPackageViewer.Help;'
$text = Read-Text $path
if ($text -notmatch 'AttachDistributorHelp\(this, "dist-viewer"\)') {
    $old = '            InitializeComponent();'
    $new = "            InitializeComponent();`n            HelpLauncher.AttachDistributorHelp(this, `"dist-viewer`");"
    if (-not $text.Contains($old)) { throw "Could not patch MainWindow constructor." }
    $text = $text.Replace($old,$new)
}
if ($text -notmatch 'private void HelpButton_Click') {
    $marker = '        private void MinimizeButton_Click'
    $method = @'
        private void HelpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HelpLauncher.ShowDistributorHelp(
                this,
                "dist-viewer");
        }

'@
    if (-not $text.Contains($marker)) { throw "Could not add viewer HelpButton_Click." }
    $text = $text.Replace($marker,$method+$marker)
}
Write-Text -Path $path -Text $text

# ------------------------------------------------------------------
# License Generator: F1 + visible Help button.
# ------------------------------------------------------------------
$path = Join-Path $repoRoot "WebPackageLicenseGenerator\MainWindow.xaml"
$old = @'
   <Button Grid.Column="1"
           Width="160"
           Height="38"
           FontWeight="SemiBold"
           Content="Generate License..."
           Click="GenerateLicenseButton_Click"/>
'@
$new = @'
   <StackPanel Grid.Column="1"
               Orientation="Horizontal">
    <Button Width="80"
            Height="38"
            Margin="0,0,8,0"
            Content="Help"
            Click="HelpButton_Click"/>
    <Button Width="160"
            Height="38"
            FontWeight="SemiBold"
            Content="Generate License..."
            Click="GenerateLicenseButton_Click"/>
   </StackPanel>
'@
Replace-Exact -Path $path -Old $old -New $new `
    -AlreadyMarker 'Click="HelpButton_Click"' `
    -Description "Added Help button to Web Package License Generator."

$path = Join-Path $repoRoot "WebPackageLicenseGenerator\MainWindow.xaml.cs"
Add-Using -Path $path `
    -After 'using WebPackageViewer.CourseCatalog;' `
    -Using 'using WebPackageViewer.Help;'
$text = Read-Text $path
if ($text -notmatch 'AttachAdministratorHelp\(this, "license-generator"\)') {
    $old = '            InitializeComponent();'
    $new = "            InitializeComponent();`n            HelpLauncher.AttachAdministratorHelp(this, `"license-generator`");"
    if (-not $text.Contains($old)) { throw "Could not patch License Generator constructor." }
    $text = $text.Replace($old,$new)
}
if ($text -notmatch 'private void HelpButton_Click') {
    $marker = '        private void ManageCoursesButton_Click'
    $method = @'
        private void HelpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HelpLauncher.ShowAdministratorHelp(
                this,
                "license-generator");
        }

'@
    if (-not $text.Contains($marker)) { throw "Could not add License Generator HelpButton_Click." }
    $text = $text.Replace($marker,$method+$marker)
}
Write-Text -Path $path -Text $text

# ------------------------------------------------------------------
# Subwindows: F1 to context-relevant administrator help.
# ------------------------------------------------------------------
$subwindows = @(
    @{
        Path = "WebPackageLicenseGenerator\ManageCoursesWindow.xaml.cs";
        UsingAfter = 'using WebPackageViewer.CourseCatalog;';
        Topic = 'course-catalog';
        ConstructorNeedle = "            InitializeComponent();`n            Reload();";
        ConstructorReplacement = "            InitializeComponent();`n            Reload();`n            HelpLauncher.AttachAdministratorHelp(this, `"course-catalog`");"
    },
    @{
        Path = "WebPackageLicenseGenerator\InstalledLicensesWindow.xaml.cs";
        UsingAfter = 'using WebPackageViewer.Licensing;';
        Topic = 'installed-licenses';
        ConstructorNeedle = "            InitializeComponent();`n            Reload();";
        ConstructorReplacement = "            InitializeComponent();`n            Reload();`n            HelpLauncher.AttachAdministratorHelp(this, `"installed-licenses`");"
    },
    @{
        Path = "WebPackageLicenseGenerator\OperatorSetupWindow.xaml.cs";
        UsingAfter = 'using WebPackageViewer.CourseCatalog;';
        Topic = 'operator-setup';
        ConstructorNeedle = "            InitializeComponent();`n            RefreshStatus();";
        ConstructorReplacement = "            InitializeComponent();`n            RefreshStatus();`n            HelpLauncher.AttachAdministratorHelp(this, `"operator-setup`");"
    }
)

foreach ($item in $subwindows) {
    $path = Join-Path $repoRoot $item.Path
    Add-Using -Path $path -After $item.UsingAfter -Using 'using WebPackageViewer.Help;'
    $text = Read-Text $path
    if ($text -notmatch [regex]::Escape("AttachAdministratorHelp(this, `"$($item.Topic)`")")) {
        if (-not $text.Contains($item.ConstructorNeedle)) {
            # Try LF-only representation.
            $needle = $item.ConstructorNeedle.Replace("`n","`n")
            $replacement = $item.ConstructorReplacement.Replace("`n","`n")
            if (-not $text.Contains($needle)) {
                throw "Could not patch help into $($item.Path)."
            }
            $text = $text.Replace($needle,$replacement)
        }
        else {
            $text = $text.Replace($item.ConstructorNeedle,$item.ConstructorReplacement)
        }
        Write-Text -Path $path -Text $text
    }
}

# CourseEditor has constructor arguments; attach immediately after InitializeComponent.
$path = Join-Path $repoRoot "WebPackageLicenseGenerator\CourseEditorWindow.xaml.cs"
Add-Using -Path $path `
    -After 'using WebPackageViewer.CourseCatalog;' `
    -Using 'using WebPackageViewer.Help;'
$text = Read-Text $path
if ($text -notmatch 'AttachAdministratorHelp\(this, "course-catalog"\)') {
    $old = '            InitializeComponent();'
    $new = "            InitializeComponent();`n            HelpLauncher.AttachAdministratorHelp(this, `"course-catalog`");"
    if (-not $text.Contains($old)) { throw "Could not patch CourseEditorWindow." }
    $text = $text.Replace($old,$new)
    Write-Text -Path $path -Text $text
}

# Backup password window.
$path = Join-Path $repoRoot "WebPackageLicenseGenerator\BackupPasswordWindow.xaml.cs"
Add-Using -Path $path `
    -After 'using System.Windows;' `
    -Using 'using WebPackageViewer.Help;'
$text = Read-Text $path
if ($text -notmatch 'AttachAdministratorHelp\(this, "signing-backup"\)') {
    $old = '            InitializeComponent();'
    $new = "            InitializeComponent();`n            HelpLauncher.AttachAdministratorHelp(this, `"signing-backup`");"
    if (-not $text.Contains($old)) { throw "Could not patch BackupPasswordWindow." }
    $text = $text.Replace($old,$new)
    Write-Text -Path $path -Text $text
}

# ------------------------------------------------------------------
# Add both external HTML manuals to the Visual Studio Setup Project.
# Administrator help remains external and internal-only.
# ------------------------------------------------------------------
$setupPath =
    Join-Path $repoRoot "WebPackageTools.Setup\WebPackageTools.Setup.vdproj"

if (Test-Path $setupPath) {
    $setup = Read-Text $setupPath

    if ($setup -notmatch 'WebPackageTools-Administrator\.html') {
        $viewerBlock = [regex]::Match(
            $setup,
            '(?s)"SourcePath" = "8:\.\.\\\\WebPackageViewer\.exe".*?"Folder" = "8:([^\"]+)"')

        if (-not $viewerBlock.Success) {
            throw "Could not determine the Setup Project Application Folder."
        }

        $appFolderId = $viewerBlock.Groups[1].Value

        $fileOpen = @'
        "File"
        {
'@

        if (-not $setup.Contains($fileOpen)) {
            throw "Could not locate the File section in WebPackageTools.Setup.vdproj."
        }

        $helpFiles = @"
            "{1FB2D0AE-D3B9-43D4-B9DD-F88EC61E35DE}:_A8F1D7B6A2794FE5B80862C60E5A88D1"
            {
            "SourcePath" = "8:..\\Documentation\\WebPackageTools-Administrator.html"
            "TargetName" = "8:WebPackageTools-Administrator.html"
            "Tag" = "8:"
            "Folder" = "8:$appFolderId"
            "Condition" = "8:"
            "Transitive" = "11:FALSE"
            "Vital" = "11:TRUE"
            "ReadOnly" = "11:FALSE"
            "Hidden" = "11:FALSE"
            "System" = "11:FALSE"
            "Permanent" = "11:FALSE"
            "SharedLegacy" = "11:FALSE"
            "PackageAs" = "3:1"
            "Register" = "3:1"
            "Exclude" = "11:FALSE"
            "IsDependency" = "11:FALSE"
            "IsolateTo" = "8:"
            }
            "{1FB2D0AE-D3B9-43D4-B9DD-F88EC61E35DE}:_F0F11A8D82D64C1695E835483339804D"
            {
            "SourcePath" = "8:..\\Documentation\\WebPackageCourses-Distributor.html"
            "TargetName" = "8:WebPackageCourses-Distributor.html"
            "Tag" = "8:"
            "Folder" = "8:$appFolderId"
            "Condition" = "8:"
            "Transitive" = "11:FALSE"
            "Vital" = "11:TRUE"
            "ReadOnly" = "11:FALSE"
            "Hidden" = "11:FALSE"
            "System" = "11:FALSE"
            "Permanent" = "11:FALSE"
            "SharedLegacy" = "11:FALSE"
            "PackageAs" = "3:1"
            "Register" = "3:1"
            "Exclude" = "11:FALSE"
            "IsDependency" = "11:FALSE"
            "IsolateTo" = "8:"
            }
"@

        $setup =
            $setup.Replace(
                $fileOpen,
                $fileOpen + $helpFiles)

        Write-Text -Path $setupPath -Text $setup
        Write-Host "Added both help manuals to the MSI Application Folder." -ForegroundColor Green
    }
    else {
        Write-Host "Setup project already contains the help manuals."
    }
}
else {
    Write-Host "Setup project not found; MSI help-file update skipped." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Stage 8 help integration complete." -ForegroundColor Green
Write-Host "Next: Visual Studio -> Release -> Rebuild Solution"
