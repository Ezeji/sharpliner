﻿using System.IO;
using Sharpliner.AzureDevOps;

namespace Sharpliner.CI
{
    internal class PublishPipeline : SingleStageAzureDevOpsPipelineDefinition
    {
        public override string TargetFile => "publish.yml";

        public override TargetPathType TargetPathType => TargetPathType.RelativeToGitRoot;

        public override SingleStageAzureDevOpsPipeline Pipeline => new()
        {
            Jobs =
            {
                new Job("Publish", "Publish to nuget.org")
                {
                    Pool = new HostedPool("Azure Pipelines", "windows-latest"),
                    Steps =
                    {
                        Powershell.FromResourceFile("Get-Version.ps1").DisplayAs("Detect package version"),

                        Task("UseDotNet@2", "Install .NET 6 preview 3") with
                        {
                            Inputs = new()
                            {
                                { "packageType", "sdk" },
                                { "version", "6.0.100-preview.3.21202.5" },
                            }
                        },

                        Powershell.Inline("New-Item -Path 'artifacts' -Name 'packages' -ItemType 'directory'"),

                        Task("DotNetCoreCLI@2", "Build Sharpliner.csproj") with
                        {
                            Inputs = new()
                            {
                                { "command", "build" },
                                { "includeNuGetOrg", true },
                                { "projects", "src/Sharpliner/Sharpliner.csproj" },
                                { "arguments", "-c Release" },
                            }
                        },

                        Task("NuGetCommand@2", "Pack the .nupkg") with
                        {
                            Inputs = new()
                            {
                                { "command", "pack" },
                                { "includeNuGetOrg", true },
                                { "packagesToPack", "src/Sharpliner/Sharpliner.csproj" },
                                { "versioningScheme", "byPrereleaseNumber" },
                                { "majorVersion", "$(majorVersion)" },
                                { "minorVersion", "$(minorVersion)" },
                                { "patchVersion", "$(patchVersion)" },
                                { "configuration", "Release" },
                            }
                        },

                        If.And(IsNotPullRequest, IsBranch("refs/heads/main"))
                            .Step(Task("NuGetAuthenticate@0", "Authenticate NuGet"))
                            .Step(Task("NuGetCommand@2", "Publish to nuget.org") with
                            {
                                Inputs = new()
                                {
                                    { "command", "push" },
                                    { "packagesToPush", "artifacts/packages/Sharpliner*.nupkg" },
                                    { "nuGetFeedType", "external" },
                                    { "publishFeedCredentials", "Sharpliner / nuget.org" },
                                }
                            })
                    }
                }
            },
        };
    }
}