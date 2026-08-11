using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using SOACS.Arsenal.Models;

namespace SOACS.Arsenal.Services
{
 public sealed class DeploymentBuildProgress
 {
  public int Percent { get; set; }
  public string Phase { get; set; }
  public string CurrentFile { get; set; }
  public string Message { get; set; }
  public long BytesProcessed { get; set; }
  public long TotalBytes { get; set; }
  public int FilesProcessed { get; set; }
  public int TotalFiles { get; set; }
 }

 public sealed class DeploymentBuildResult
 {
  public string PackageFolder { get; set; }
  public string ZipPath { get; set; }
  public int RepositoryFileCount { get; set; }
  public int RuntimeFileCount { get; set; }
  public string ManifestPath { get; set; }
  public long ZipSizeBytes { get; set; }
  public string DeploymentId { get; set; }
 }

 public sealed class DeploymentPackageBuilder
 {
  public DeploymentBuildResult Build(string repositoryPath, string destinationRoot, IEnumerable<PatchItem> plan, IProgress<DeploymentBuildProgress> progress = null)
  {
   if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath)) throw new DirectoryNotFoundException("The selected patch repository does not exist.");
   if (string.IsNullOrWhiteSpace(destinationRoot)) throw new ArgumentException("A deployment destination is required.");

   var repoFull = Path.GetFullPath(repositoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
   var destFull = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
   if (destFull.StartsWith(repoFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("The deployment destination cannot be inside the source patch repository.");

   Directory.CreateDirectory(destFull);
   var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
   var deploymentId = "ARS-" + DateTime.Now.ToString("yyyy-MM") + "-" + DateTime.Now.ToString("ddHHmmss");
   var root = Path.Combine(destFull, "SOACS_Arsenal_Deployment_" + stamp);
   var repositoryDir = Path.Combine(root, "Repository");
   Directory.CreateDirectory(repositoryDir);
   Directory.CreateDirectory(Path.Combine(root, "Rules"));

   var sourceFiles = Directory.GetFiles(repoFull, "*", SearchOption.AllDirectories);
   long totalSourceBytes = sourceFiles.Sum(f => new FileInfo(f).Length);
   long bytesDone = 0;
   int filesDone = 0;
   Report(progress, 1, "Preparing", "", "Creating deployment folder...", 0, totalSourceBytes, 0, sourceFiles.Length);

   int repoCount = CopyTreeWithProgress(repoFull, repositoryDir, sourceFiles, ref bytesDone, ref filesDone, totalSourceBytes, progress);
   int runtimeCount = 0;

   Report(progress, 58, "Rules", "ApplicationRules.xml", "Copying application intelligence rules...", bytesDone, totalSourceBytes, filesDone, sourceFiles.Length);
   CopyRules(root);
   Report(progress, 61, "Runner", "Invoke-ArsenalDeployment.ps1", "Adding standalone PowerShell deployment runner...", bytesDone, totalSourceBytes, filesDone, sourceFiles.Length);
   CopyDeploymentScript(root);

   var planItems = (plan ?? Enumerable.Empty<PatchItem>()).OrderBy(x => x.Sequence).ToList();
   Report(progress, 65, "Manifest", "PatchManifest.xml", "Generating ordered deployment manifest...", bytesDone, totalSourceBytes, filesDone, sourceFiles.Length);
   var manifest = new XDocument(
    new XElement("ArsenalDeployment",
     new XAttribute("SchemaVersion", "1.2"),
     new XAttribute("DeploymentId", deploymentId),
     new XAttribute("CreatedUtc", DateTime.UtcNow.ToString("o")),
     new XAttribute("CreatedBy", Environment.UserDomainName + "\\" + Environment.UserName),
     new XAttribute("Computer", Environment.MachineName),
     new XElement("SourceRepository", repoFull),
     new XElement("PackageVersion", "1.5.4 Alpha"),
     new XElement("RepositoryFileCount", repoCount),
     new XElement("InstallPlan", planItems.Select(p =>
      new XElement("Package",
       new XAttribute("Sequence", p.Sequence),
       new XAttribute("Category", p.Category ?? ""),
       new XAttribute("Automated", p.CanAutomate),
       new XAttribute("HashStatus", p.HashStatus ?? ""),
       new XElement("RelativePath", SafeRelative(repoFull, p.FullPath)),
       new XElement("FileName", p.FileName ?? ""),
       new XElement("Rule", p.RuleName ?? ""),
       new XElement("ExpectedSHA256", p.ExpectedHash ?? ""),
       new XElement("CalculatedSHA256", p.CalculatedHash ?? ""),
       new XElement("Disposition", p.Status ?? ""),
       new XElement("Result", p.Result ?? ""))))));
   var manifestPath = Path.Combine(root, "PatchManifest.xml");
   manifest.Save(manifestPath);

   Report(progress, 68, "Launcher", "Deploy.cmd", "Creating administrator launcher and instructions...", bytesDone, totalSourceBytes, filesDone, sourceFiles.Length);
   File.WriteAllText(Path.Combine(root, "Deploy.cmd"),
    "@echo off\r\nsetlocal\r\ncd /d \"%~dp0\"\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0Invoke-ArsenalDeployment.ps1\"\r\npause\r\nendlocal\r\n", Encoding.ASCII);
   File.WriteAllText(Path.Combine(root, "DeploymentReadMe.txt"),
    "SOACS Arsenal Standalone Offline Deployment Package\r\n\r\nDeployment ID: " + deploymentId + "\r\n\r\nThe target computer does not need SOACS Arsenal installed.\r\n\r\n1. Copy or extract the complete package to the target system.\r\n2. Right-click Deploy.cmd and run as administrator.\r\n3. The PowerShell runner validates SHA-256 hashes, follows the ordered install plan, applies supported packages silently, provisions Store apps for all users, and writes logs/results.\r\n4. Packages without a recognized rule remain manual. Firmware is blocked unless explicitly authorized after qualification.\r\n\r\nDo not remove dependency, license, manifest, rule, or hash files.\r\n", Encoding.UTF8);

   var sumsPath = Path.Combine(root, "SHA256SUMS.txt");
   Report(progress, 72, "Integrity", "SHA256SUMS.txt", "Calculating SHA-256 values for deployment files...", bytesDone, totalSourceBytes, filesDone, sourceFiles.Length);
   WriteHashManifest(root, sumsPath, progress, 72, 84);

   var zipPath = root + ".zip";
   if (File.Exists(zipPath)) File.Delete(zipPath);
   Report(progress, 85, "Compression", Path.GetFileName(zipPath), "Compressing standalone deployment ZIP...", bytesDone, totalSourceBytes, filesDone, sourceFiles.Length);
   CreateZipWithProgress(root, zipPath, progress, 85, 98);

   Report(progress, 98, "Verification", Path.GetFileName(zipPath), "Verifying ZIP integrity...", bytesDone, totalSourceBytes, filesDone, sourceFiles.Length);
   VerifyZip(zipPath);

   // The expanded deployment folder is only a staging area. Once the archive has
   // been verified, remove it so the destination contains one deployable copy.
   var zipSize = new FileInfo(zipPath).Length;
   Report(progress, 99, "Cleanup", Path.GetFileName(root), "Removing temporary deployment staging folder...", bytesDone, totalSourceBytes, filesDone, sourceFiles.Length);
   Directory.Delete(root, true);

   Report(progress, 100, "Complete", Path.GetFileName(zipPath), "Deployment ZIP complete and verified.", totalSourceBytes, totalSourceBytes, sourceFiles.Length, sourceFiles.Length);

   return new DeploymentBuildResult { PackageFolder = destFull, ZipPath = zipPath, RepositoryFileCount = repoCount, RuntimeFileCount = runtimeCount, ManifestPath = "PatchManifest.xml (inside ZIP)", ZipSizeBytes = zipSize, DeploymentId = deploymentId };
  }

  static int CopyTreeWithProgress(string source, string destination, string[] files, ref long bytesDone, ref int filesDone, long totalBytes, IProgress<DeploymentBuildProgress> progress)
  {
   Directory.CreateDirectory(destination);

   // Ref/out parameters cannot be captured by an anonymous method. Keep local
   // counters for the buffered-copy callback, then copy the final values back.
   long localBytesDone = bytesDone;
   int localFilesDone = filesDone;

   foreach (var file in files)
   {
    var relative = SafeRelative(source, file);
    var target = Path.Combine(destination, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    CopyFileBuffered(file, target, delegate(long delta)
    {
     localBytesDone += delta;
     int percent = totalBytes <= 0 ? 55 : 3 + (int)Math.Min(52, (localBytesDone * 52L / totalBytes));
     Report(progress, percent, "Repository", relative, "Copying repository files...", localBytesDone, totalBytes, localFilesDone, files.Length);
    });
    localFilesDone++;
    Report(progress, totalBytes <= 0 ? 55 : 3 + (int)Math.Min(52, (localBytesDone * 52L / totalBytes)), "Repository", relative, "Copied " + localFilesDone + " of " + files.Length + " files", localBytesDone, totalBytes, localFilesDone, files.Length);
   }

   bytesDone = localBytesDone;
   filesDone = localFilesDone;
   return localFilesDone;
  }

  static void CopyFileBuffered(string source, string target, Action<long> reportBytes)
  {
   const int bufferSize = 1024 * 1024;
   using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan))
   using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan))
   {
    var buffer = new byte[bufferSize];
    int read;
    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
    {
     output.Write(buffer, 0, read);
     if (reportBytes != null) reportBytes(read);
    }
   }
  }

  static void CopyDeploymentScript(string root)
  {
   var source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Deployment", "Invoke-ArsenalDeployment.ps1");
   if (!File.Exists(source)) throw new FileNotFoundException("Standalone deployment script is missing from the Arsenal runtime.", source);
   File.Copy(source, Path.Combine(root, "Invoke-ArsenalDeployment.ps1"), true);
  }

  static void CopyRules(string root)
  {
   var source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Rules");
   if (Directory.Exists(source)) CopyTree(source, Path.Combine(root, "Rules"), null);
  }

  static int CopyTree(string source, string destination, Func<string, bool> filter)
  {
   Directory.CreateDirectory(destination); int count = 0;
   foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
   {
    if (filter != null && !filter(file)) continue;
    var relative = SafeRelative(source, file);
    var target = Path.Combine(destination, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    File.Copy(file, target, true); count++;
   }
   return count;
  }

  static void WriteHashManifest(string root, string outputPath, IProgress<DeploymentBuildProgress> progress, int startPercent, int endPercent)
  {
   var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => !string.Equals(f, outputPath, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
   var lines = new List<string>();
   for (int i = 0; i < files.Length; i++)
   {
    var file = files[i];
    using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file))
    {
     var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
     lines.Add(hash + " *" + SafeRelative(root, file).Replace('\\', '/'));
    }
    int pct = startPercent + (files.Length == 0 ? 0 : (i + 1) * (endPercent - startPercent) / files.Length);
    Report(progress, pct, "Integrity", SafeRelative(root, file), "Hashing deployment files...", i + 1, files.Length, i + 1, files.Length);
   }
   File.WriteAllLines(outputPath, lines, Encoding.ASCII);
  }

  static void CreateZipWithProgress(string sourceFolder, string zipPath, IProgress<DeploymentBuildProgress> progress, int startPercent, int endPercent)
  {
   var files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
   long total = files.Sum(f => new FileInfo(f).Length);
   long done = 0;
   using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
   {
    for (int i = 0; i < files.Length; i++)
    {
     var file = files[i];
     var relative = SafeRelative(sourceFolder, file).Replace('\\', '/');
     var entry = archive.CreateEntry(relative, CompressionLevel.Optimal);
     using (var input = File.OpenRead(file))
     using (var output = entry.Open())
     {
      var buffer = new byte[1024 * 1024];
      int read;
      while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
      {
       output.Write(buffer, 0, read);
       done += read;
       int pct = startPercent + (total <= 0 ? 0 : (int)(done * (endPercent - startPercent) / total));
       Report(progress, pct, "Compression", relative, "Compressing ZIP...", done, total, i, files.Length);
      }
     }
    }
   }
  }

  static void VerifyZip(string zipPath)
  {
   using (var archive = ZipFile.OpenRead(zipPath))
   {
    foreach (var entry in archive.Entries)
    {
     if (string.IsNullOrEmpty(entry.Name)) continue;
     using (var stream = entry.Open())
     {
      var buffer = new byte[8192];
      while (stream.Read(buffer, 0, buffer.Length) > 0) { }
     }
    }
   }
  }

  static void Report(IProgress<DeploymentBuildProgress> progress, int percent, string phase, string currentFile, string message, long bytesProcessed, long totalBytes, int filesProcessed, int totalFiles)
  {
   if (progress == null) return;
   progress.Report(new DeploymentBuildProgress { Percent = Math.Max(0, Math.Min(100, percent)), Phase = phase, CurrentFile = currentFile, Message = message, BytesProcessed = bytesProcessed, TotalBytes = totalBytes, FilesProcessed = filesProcessed, TotalFiles = totalFiles });
  }

  static string SafeRelative(string root, string path)
  {
   if (string.IsNullOrWhiteSpace(path)) return "";
   var rootUri = new Uri(AppendSlash(Path.GetFullPath(root)));
   var pathUri = new Uri(Path.GetFullPath(path));
   return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
  }
  static string AppendSlash(string path) { return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar; }
 }
}
