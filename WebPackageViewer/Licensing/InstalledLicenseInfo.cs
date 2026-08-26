namespace WebPackageViewer.Licensing
{
    public sealed class InstalledLicenseInfo
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public string Distributor { get; set; }
        public string MachineId { get; set; }
        public string Expires { get; set; }
        public string Status { get; set; }
        public string Storage { get; set; }
        public string FilePath { get; set; }
        public bool IsDisabled { get; set; }
    }
}
