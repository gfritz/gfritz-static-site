---
layout: post
title: "FsAdvent 2019 - Using FAKE in a Build Server"
published: "2019-12-24"
---

# Using FAKE in a Build Server

## F# Advent 2019

This is a post for [F# Advent 2019](https://sergeytihon.com/2019/11/05/f-advent-calendar-in-english-2019/) facilitated by Sergey Tihon. Visit the link to see many more posts about F#.

## Integrating with TeamCity

This article will be TeamCity specific, but there is not much configuration needed to use FAKE.

In short, configure your build agent to run your FAKE `*.fsx` script, and have your script pull in build agent variables, like nuget feeds, docker feeds, credentials, build counter, via environment variables.

Always try to write your scripts to be [build server agnostic](https://fake.build/buildserver.html#General-API-usage). Even isolating a build server specific dependency behind a function is better than not isolating the dependency at all.

To use FAKE, your build server needs at least one of the following on one or more of its build agents:

* install .NET SDK 2.1+ on your build agent for dotnet tool support, or
* install Docker on your build agent and specify a Dockerfile for your build agent dependencies.

Add these lines to your build script to integrate with TeamCity:

```fsharp
open Fake.Core
open Fake.BuildServer

BuildServer.install [ TeamCity.Installer ]
```

Next, modify your TeamCity configuration:

* Select Runner Type = Command Line,
* Name the step something like "Run FAKE script", or whatever you like,
* Execute step = "If all previous steps finished successfully",
* Run = "Custom Script",
* Custom Scripts = `fake build target CIBuild`,
* Format stderr output as = "error",
* Run step within Docker container = "name of the image you built from your dockerfile":
  * Hopefully you have an internal docker registry to host docker images.
  * Alternatively, you can choose Runner Type = "Docker" and specify the Dockerfile in your repository, but this will build the dockerfile every time.

## Build Versions and Release Notes

My teammates really like this feature of FAKE. We follow the "Complex Format" per the [FAKE release notes module documentation](https://fake.build/apidocs/v5/fake-core-releasenotes.html) with one small difference.

RELEASE_NOTES.md:

```md
// FAKE's complex format
## New in 1.2.1 (Released 2019/12/24)
* stuff
* and things too

// what we do instead
## 1.2.1 - 24-Dec-2019
* stuff
* and things too
```

The version number of the artifacts are determined from the source code. The build server only provides a number that increments on each build.

Our build numbers follow the `Major.Minor.Patch.Revision` format where Major, Minor, and Patch are sourced using the `Fake.ReleaseNotes` module with a `RELEASE_NOTES.md` file. The Revision is the TeamCity build counter.

You can think of the build script as a function that takes in an argument for `Revision` and assumes it runs in a git repository. Note that anything could provide the `Revision` argument, but the build script will load that from an environment variable.

If you want to overly simplify a build script to a function, this is close-ish:

```fsharp
FileSystem -> DockerFeedConnection -> NugetFeedConnection -> RevisionNumber  -> unit
```

## NuGet Packages

```fsharp
// testTask.IfNeeded means THIS task should run after
let nugetPackTask = BuildTask.create "Artifact" [ testTask.IfNeeded ] {

    let nugetPackDefaults = fun (options : NuGet.NuGetParams) ->
        // tool path is by default ./tools/ or you can change it with Tools = "/path/to/nuget.exe"
        { options with
            Publish = true
            PublishUrl = "https://artifacts.company.com/api/nuget/v3/"
            // https://fake.build/dotnet-nuget.html#Creating-a-nuspec-template
            // replace placeholders in .nuspec with `NuGetParams` record field
            Version = EV.version()
            Authors = authors
            Summary = "A super cool dotnet core application."
            Description = "A longer description about this super cool dotnet core application."
            ReleaseNotes = release().Notes |> String.toLines
            // FS0052 workaround (ugly: let x = ... in x); this is a shorthand to make an intermediate value
            Copyright = sprintf "Your Company %i" (let now = System.DateTime.UtcNow in now.Year)
            Tags = "C#;F#;FAKE;"
            Files = [   // projects deploying to kubernetes should insert their own yml file,
                        // but these files should always be packaged
                        "fake.cmd", Some "content", None
                        "fake.sh", Some "content", None
                        "deploy.fsx", Some "content", None
                        "paket.dependencies", Some "content", None
                        "paket.lock", Some "content", None ]
            // set paths for NuGet
            OutputPath = artifactOutDir
            WorkingDir = buildOutDir
            BasePath = Some root }

    let packApi () =
        // take the nuget pack defaults and apply API specific nuget pack settings
        NuGet.NuGet (nugetPackDefaults >> ApiProject.nugetPackSettings) ".nuspec"

    // now pack them all (could async parallel this later)
    packApi ()
}
```

If you noticed `ApiProject.nugetPackSettings`, I like to put all functions, values, paths, and names specific for a project into a project specific module in the build script.

## Docker Images

```fsharp
//
// Helpers
//

/// Look for the specified `tool` on the Environment's PATH and in `otherSearchFolders`.
/// - `tool` : name of the tool on a *nix system
/// - `winTool` : name of the executable on a windows system
let platformTool tool winTool otherSearchFolders =
    let tool = if Environment.isLinux then tool else winTool
    tool
    |> ProcessUtils.tryFindFileOnPath
    |> function
        | Some pathTool -> pathTool
        | None ->
            if Seq.isEmpty otherSearchFolders then
                failwithf "platformTool %s not found" tool
            else
                ProcessUtils.tryFindFile otherSearchFolders tool
                |> function
                    | Some folderTool -> folderTool
                    | None -> failwithf "folderTool %s not found in folders %A" tool otherSearchFolders

let dockerTool =
    // you should have it installed on your development machine
    // we assume docker is included in the build agent path too
    platformTool "docker" "docker.exe" Seq.empty

let buildDocker repositoryUrl tag =
    let args = sprintf "build -t %s ." (repositoryUrl </> tag)
    runTool "docker" args "."

let pushDocker repositoryUrl tag =
    let args = sprintf "push %s" (repositoryUrl </> tag)
    runTool "docker" args "."

let dockerUser = "yourcompany-user"
let dockerImageName = "yourcompany-api"
let dockerFullName = sprintf "%s/%s:%s" dockerUser dockerImageName (EV.buildVersion())

let dockerBuildTask = BuildTask.create "DockerBuild" [] {
    buildDocker Docker.repositoryUrl dockerFullName
}
// publish the docker image
let dockerBuildTask = BuildTask.create "DockerPush" [dockerBuildTask] {
    pushDocker Docker.repositoryUrl dockerFullName
}

```

## Stringly vs Strongly Typed Build Targets

### Stringly Typed

FAKE by default has you define build targets like so:

```fsharp
open Fake.Core

Target.initializeEnvironment()

// define targets
Target.create "Test" (fun _ ->
    // run dotnet test, or whatever
)
Target.create "Publish" (fun _ ->
    // run dotnet publish
)
Target.create "Default" (fun _ ->
    // an empty task for default build behavior on a developer machine
)
Target.create "CI" (fun _ ->
    // an empty task for the CI server to enter the CI specific build target ordering
)

// define ordering
"Test"
==> "Default"

"Default"
==> "Publish"
==> "CI"

// if you run `fake build`, then "Default" will be the starting target
Target.runOrDefault "Default"

```

### Strongly Typed

[vbfox](https://blog.vbfox.net/2018/09/12/fake-typed-targets.html) created a FAKE 5 module for [strongly-typed targets](https://github.com/vbfox/FoxSharp/tree/master/src/BlackFox.Fake.BuildTask) that allows scripts to define let-bound values that represent build tasks, and the compiler will be able to check the usage of those targets like any other normal value.

I use `BlackFox.Fake`, but I miss the summary-like expression listing the order of build targets. For example:

```fsharp
//// Fake.Core.Target

// define targets
Target.create "Clean" ()
Target.create "Test" ()
Target.create "Publish" ()
Target.create "CI" ()

// define ordering
"Clean"
==> "Test"
==> "Publish"

"Publish"
==> "CI"

//// BlackFox.Fake.BuildTask

let cleanTask = BuildTask.create "Clean" [] { (* *) }
let testTask = BuildTask.create "Test" [clean.IfNeeded] { (* *) }
let publishTask = BuildTask.create "Publish" [testTask] { (* *) }
let ciTask = BuildTask.create "CI" [publishTask] { (* *) }
```

I do not have a clear preference or advice on what to choose over the other. I suggest trying for yourself. My day-to-day build target order is not complicated enough to show a clear difference.

## Creating Octopus Releases

If you use something other than Octopus, chances are your deployment server has a REST API to create and deploy releases.

```fsharp

let projectName = "Some Service"

module DeploymentServer =

    module private EnvironmentVariables =
        let server = Environment.environVar "Octopus-Server"
        let apiKey = Environment.environVar "Octopus-TeamCityAPIKey"

    [<AutoOpen>]
    module private Helpers =
        // when Fake.Tools.Octo nuget package works with dotnet tool Octopus.DotNet.Cli, use Fake.Octo instead
        let octoTool cmd args =
            dotnetTool (sprintf "octo %s %s --server=%s --apikey=%s" cmd args EnvironmentVariables.server EnvironmentVariables.apiKey) "."

    let private createReleaseArgs =
        // Using triple quotes to allow for quote characters in the format string, also could have escaped with backslash.
        // Re-use your release notes so you see them in the octopus release screen.
        sprintf """--package=*:%s --project="%s" --version="%s" --releasenotesfile="%s" """ buildNumber projectName buildNumber releaseNotesFile

    /// Creates a release in Octopus for this build
    let createRelease _ =
        // dotnet tool update will: 1. install if not installed, 2. same version installed, reinstall it, 3. update lower version to current version
        // This is nice because we do not have to check if the tool is already installed and conditionally NOT run `dotnet tool install` if it is. Install fails if the tool is already installed.
        // https://github.com/dotnet/cli/pull/10205#issuecomment-506847148
        dotnetTool "tool update -g Octopus.DotNet.Cli" "."
        octoTool "create-release" createReleaseArgs

// make sure when this task runs that any nuget packages, docker images, etc. are already published
BuildTask.create "CreateRelease" [yourNugetPublishTask; yourDockerPublishTask] {
    DeploymentServer.createRelease
}
```

## VS Code Dev Containers

A good way to shorten the feedback loop on your Dockerfile defining your build dependencies is to use that Dockerfile locally. VS Code's Dev Container feature makes that really easy provided you have Docker and VS Code installed.

I have two unsolved-by-me, but manageable, problems with this approach:

* .fake/ cache sometimes picks up as "invalid" so I have to purge the directory and download dependencies again
* paket-files/ sometimes experiences the same behavior as .fake/

I may have done something wrong with my Dockerfile/fake/paket combination. I have not investigated much because this problem does not happen often enough to waste time.

```dockerfile
##
## want dotnet-sdk to use dotnet-tool and run the build script with dotnet-fake
##
FROM mcr.microsoft.com/dotnet/core/sdk:3.0-alpine
RUN apk update
# add dotnet tools to path to pick up fake and paket installation
ENV PATH="${PATH}:/root/.dotnet/tools"
# install dotnet tools for fake, paket, octopus
RUN    dotnet tool install -g fake-cli \
    && dotnet tool install -g paket \
# https://octopus.com/docs/octopus-rest-api/octo.exe-command-line/install-global-tool
    && dotnet tool install -g Octopus.DotNet.Cli \
# install yarn
    && apk add yarn \
# install docker cli; note the build server will have to provide the actual docker engine
    && apk add docker \
# other tools expected by build.*.fsx scripts
    && apk add git curl
# bring in the build scripts and build script dependencies files
COPY build.standalone.fsx build.webcomponents.fsx paket.dependencies paket.lock /var/app/
COPY .paket /var/app/.paket/WORKDIR /var/app
```

I publish this image to our docker registry my teammates and the build server do not need to rebuild the image every time. 

## FAKE and Build Servers

Try to write build scripts to be build server agnostic.

While we do not change our build server, we gain the ability to treat our build process as just another segment of code to branch, peer review, and run. I think this is much easier than using pre-defined steps and templates defined in your build server of choice.

## Links, Inspiration, and Contact

View the other [F# Advent 2019 posts](https://sergeytihon.com/2019/11/05/f-advent-calendar-in-english-2019/)!

Links:

* [FAKE](https://fake.build/)
* [BlackFox.Fake.BuildTask](https://github.com/vbfox/FoxSharp/tree/master/src/BlackFox.Fake.BuildTask)
* [VS Code Dev Containers - Getting Started](https://code.visualstudio.com/docs/remote/containers#_getting-started)

Inspiration:

I often reviewed these repositories to see how they used FAKE.

* [Ionide](https://github.com/ionide/ionide-vscode-fsharp)
* [SAFE Stack BookStore](https://github.com/SAFE-Stack/SAFE-BookStore)

Contact:

I do not have a comments section, so please use [@garthfritz on Twitter](https://twitter.com/garthfritz/) or `@garth` on the [F# Software Foundation Slack](https://fsharp.org/guides/slack/) to contact me with feedback or clarification.
