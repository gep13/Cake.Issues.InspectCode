#load nuget:?package=Cake.Recipe&version=1.0.0

Environment.SetVariableNames();

BuildParameters.SetParameters(
    context: Context, 
    buildSystem: BuildSystem,
    sourceDirectoryPath: "./src",
    title: "Cake.Issues.InspectCode",
    repositoryOwner: "cake-contrib",
    repositoryName: "Cake.Issues.InspectCode",
    appVeyorAccountName: "cakecontrib",
    shouldRunCodecov: false);

BuildParameters.PrintParameters(Context);

ToolSettings.SetToolSettings(
    context: Context,
    dupFinderExcludePattern: new string[]
    {
        BuildParameters.RootDirectoryPath + "/src/Cake.Issues.InspectCode*/**/*.AssemblyInfo.cs",
        BuildParameters.RootDirectoryPath + "/src/Cake.Issues.InspectCode.Tests/**/*.cs"
    },
    testCoverageFilter: "+[*]* -[xunit.*]* -[Cake.Core]* -[Cake.Testing]* -[*.Tests]* -[Cake.Issues]* -[Cake.Issues.Testing]* -[Shouldly]*",
    testCoverageExcludeByAttribute: "*.ExcludeFromCodeCoverage*",
    testCoverageExcludeByFile: "*/*Designer.cs;*/*.g.cs;*/*.g.i.cs");

Build.RunDotNetCore();
