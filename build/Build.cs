using Microsoft.Build.Tasks;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
  /// Support plugins are available for:
  ///   - JetBrains ReSharper        https://nuke.build/resharper
  ///   - JetBrains Rider            https://nuke.build/rider
  ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
  ///   - Microsoft VSCode           https://nuke.build/vscode

  public static int Main() => Execute<Build>(x => x.Publish);
  //public static int Main() => Execute<Build>(x => x.Compile);

  [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
  readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

  [Solution] readonly Solution Solution;
  [GitRepository] readonly GitRepository GitRepository;

  AbsolutePath SourceDirectory => RootDirectory;
  AbsolutePath OutputDirectory => RootDirectory / "output";
  AbsolutePath PublishDirectory => RootDirectory / "publish";
  AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";
  AbsolutePath ChocolateyDirectory => RootDirectory / "chocolatey";

  [PathExecutable("choco.exe")]
  readonly Tool Chocolatey;

  Target Clean => _ => _
    .Before(Restore)
    .Executes(() =>
    {
      SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
      EnsureCleanDirectory(OutputDirectory);
      EnsureCleanDirectory(PublishDirectory);
    });

  Target CleanTestResults => _ => _
    .Before(Test)
    .Executes(() =>
    {
      EnsureCleanDirectory(TestResultsDirectory);
    });

  Target Restore => _ => _
    .Executes(() =>
    {
      // DotNetRestore will not work due to the Wix projects

      // TODO: Couldn't get Nuke to perform a NuGet restore, so punting to add the restore switch in the MSBuild task
      // NuGetTasks.NuGetRestore(s => s.SetTargetPath(Solution)); 
    });

  Target Compile => _ => _
    .DependsOn(Restore)
    .Executes(() =>
    {
      MSBuildTasks.MSBuild(c => c
        .SetRestore(true)
        .SetMSBuildPlatform(MSBuildPlatform.x86) // !! IMPORTANT !!  The VS/SMSS packages require the 32 bit version of MSBuild
        .SetProjectFile(Solution)
        .SetConfiguration(Configuration));
    });

  Target Test => _ => _
    .DependsOn(CleanTestResults)
    .DependsOn(Compile)
    .Executes(() =>
    {
      DotNetTest(s => s
        .SetProjectFile(Solution)
        .SetConfiguration(Configuration)
        .SetVerbosity(DotNetVerbosity.Normal)
        .SetLogger("trx;LogFileName=TestResults.xml")
        .SetResultsDirectory(TestResultsDirectory)
        .EnableNoBuild());
    });

  Target Publish => _ => _
    .DependsOn(Clean)
    .DependsOn(Compile)
    .Executes(() =>
    {
      // Copy SMSS Msi to output folder
      var msiSource = $"{Solution.GetProject("PoorMansTSqlFormatterSSMSPackage.Setup").Path.Parent}\\bin\\x86\\{Configuration}\\PoorMansTSqlFormatterSSMSPackage.Setup.msi";
      CopyFileToDirectory(msiSource, OutputDirectory);

      // Copy VSIX to output folder
      var vsixSource = $"{Solution.GetProject("PoorMansTSqlFormatterVSPackage2019").Path.Parent}\\bin\\{Configuration}\\PoorMansTSqlFormatterVSPackage2019.vsix";
      CopyFileToDirectory(vsixSource, OutputDirectory);
    });

  Target PublishChocolatey => _ => _
    .DependsOn(Publish)
    .Executes(() =>
    {
      Chocolatey($"pack --outputdirectory {PublishDirectory}", workingDirectory: ChocolateyDirectory);
    });
}
