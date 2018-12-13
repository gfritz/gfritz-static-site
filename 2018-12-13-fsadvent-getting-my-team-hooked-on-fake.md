---
title:  FsAdvent 2018 - Getting My Team Hooked on FAKE
date: 2018-12-13
---

This is a post for [F# Advent 2018](https://sergeytihon.com/2018/10/22/f-advent-calendar-in-english-2018/).

> "If a company says they are a ".NET shop", what they really mean is "we are a C# shop, .NET is C#, stop talking to me about F#".

&mdash; Me, ranting in my 1 on 1 meetings with my manager

I have been pushing for F# on my coworkers since I started in June 2017. Lots of things got thrown onto the wall, and the things that actually shipped were one project with JSON and WSDL Type Providers and, yesterday, a project built completely by FAKE (also has JSON and CSV Type Providers). This post will describe the things that got my team hooked on [FAKE](https://fake.build/) - an F# DSL for Build Tasks and more. This will be more narrative and opinions than F# code. Sorry. (Not sorry)

**Disclaimer**, these are opinions and are listed in no particular order. If you have any feedback, need some clarification, or want to tell me I'm completely wrong, the best place to start will be [Twitter](https://twitter.com/garthfritz).

### Things My Team Liked About FAKE

#### Feel Like a Command Line Wizard Again

If using FAKE makes developers have fun scripting building, testing, and packaging processes, then that is a win all by itself. Bonus points if it makes them feel like a cool kid.

The [FAKE dotnet global tool](https://fake.build/fake-dotnetcore.html#Getting-Started) helps with that too.

![Image of are you a wizard](https://user-images.githubusercontent.com/2267030/49901976-b26a5b00-fe30-11e8-8383-02b6dde9e08f.png)

#### Freedom to Script as Much of Build and Deploy as You Want

- the way you build locally is how the build server builds

We have the build server - TeamCity but it coudld just as well be another - provide the full build number and move our build artifacts to our internal package feed. Everything else is done in the script.

A developer can try different build configurations locally without messing up the project build configuration on the build server. Most of the benefits under this reason are the same benefits as putting any other code into source control.

The biggest win is how short the feedback cycle is for building. How quickly can you debug a build error with a particular TeamCity build step? Probably not as fast as you could on your own machine. Don't you normally remote or ssh into the problem build agent if the error log doesn't make sense anyway?

#### FAKE Features Are Cool

I have my favorite FAKE features, but these are the top ones according to my newly converted team.

- Super easy templating of .nuspec parameters

We apply the same NuGet package attributes to every assembly, so it was really easy to just let FAKE do that for us. All you have to do is substitute the values you care about and the NuGet required minimum fields.

https://fake.build/dotnet-nuget.html#Setting-up-the-build-script

- Release Notes automatically pulled from the latest version in the Release Notes file

I don't think any of our projects publish developer written release notes, but FAKE makes it easy to publish them in the NuGet package Release Notes field. I think release notes from the developer are a good idea.

https://fake.build/apidocs/v5/fake-core-releasenotes.html

> "I still don't love functional or F# for my day-to-day work, but I'll be damned if FAKE and Type Providers aren't my favorite things right now."

![Image of FAKE and Type Providers are my favorite things](https://user-images.githubusercontent.com/2267030/49901909-7c2cdb80-fe30-11e8-987e-cae2a3545ab0.png)

### Things My Team (and others) Did Not Like About FAKE

I will use the following pattern:
- problem/concern someone has
  - my not necessarily nuanced retort

- Syntax is jarring (aka syntax shock).
  - I think you mean "is not C# syntax". Well so is HTML, CSS, SQL, JavaScript, Powershell, Bash, but you can do all of those!
- Who will train other people to be familiar with F# if this becomes standard?
  - I will! So will my teammates! They are familiar enough with F#.
- Can't you just do all of this stuff in TeamCity and Octopus already? That's why we bought it.
  - Sounds like sunk cost fallacy to me. If you want finer grained control over your build I don't think that canned TeamCity steps will are enough. I think FAKE's Target Dependency Ordering is more powerful than standing up multiple TeamCity build configurations.

### How Did I Do It?

I tested out FAKE near the end of its FAKE 4 lifetime. Once FAKE updated to version 5 I tried to scrip the build for one of our big legacy applications. I did not get very far. It was way too much process to replace at once, and I could not present F# or FAKE in a good light with a partially migrated build.

Fortunately, I found an NDC talk [Immutable application deployments with F# Make - Nikolai Norman Andersen](https://www.youtube.com/watch?v=_sZT0CpJ6Vo) and Nikolai's sample [weather-in-harstad repository](https://github.com/nikolaia/weather-in-harstad) which put me on the path of making a coherent argument and demo build script for the team.

Some weeks later, we start two greenfield projects - one large in scope and one small. Here's the "secret" way I got FAKE into the build - I just did it. F# first, ask questions (or forgiveness) later, except this time it worked.

### Future Work

Due to priorities changing frequently, we have not had time to use FAKE to script our deploy process and post-deployment smoke testing. The team and I still really want to do that, but time constraints unfortunately make it smarter to just let Octopus do it's job.

Other than time constraints, I want to do some preparation work to confidently demo a solid FAKE deploy script to the team.

1. How should I pull out all of the non-sensitive variables out of Octopus and into the FAKE script?
1. Same as #1 but for the sensitive variables (API keys, Production level credentials, etc.)?

   Nikolai demonstrated using [git-secret](http://git-secret.io/) to accomplish it, but he was hesitant to recommend it. So that's why I need to research it more.
1. How do I safely and unobtrusively transform all of the former Octopus variables to their environment specific values?
1. How can I make #1-3 easy for the rest of the team to maintain and develop?
1. How do I reliably share any bespoke deployment tasks we make with other teams via Octopus?

If any of these problems sound really easy to you or you have already solved them using FAKE, please let me know!
