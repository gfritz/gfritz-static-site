﻿namespace Firm

open System
open System.IO
open Data

module Files =
    type FileData =
        { Input: string
          Output: string }

    type InputType =
        | Md
        | Fsx

    type PostFile =
        { File: FileData
          Meta: MetaData
          Name: string
          Type: InputType }

    type PageFile =
        { File: FileData
          Type: InputType }

    type ResourceFile = 
        { File: FileData }

    type InputFile =
        | PostFile of PostFile
        | PageFile of PageFile
        | ResourceFile of ResourceFile

    let (@+) path1 path2 =
        Path.Combine(path1, path2)

    let (@.) file ext =
        let fwoe = Path.GetDirectoryName(file) @+ Path.GetFileNameWithoutExtension(file)
        fwoe + ext

    let private dirSepChars = [|Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar|]

    let private postName (file: string) =
        let dirName = Path.GetDirectoryName(file)
        let dirNames = dirName.Split(dirSepChars, StringSplitOptions.RemoveEmptyEntries)
        dirNames.[Array.length dirNames - 1]

    let private relativePath (baseDir:string) (file:string) =
        let fileDirs = file.Split(dirSepChars, StringSplitOptions.RemoveEmptyEntries)
        let baseDirDirs = baseDir.Split(dirSepChars, StringSplitOptions.RemoveEmptyEntries)
        let baseDirDirsLen = Array.length baseDirDirs
        if Array.length fileDirs <= baseDirDirsLen || fileDirs.[..baseDirDirsLen - 1] <> baseDirDirs
            then failwith "Base dir must be a prefix to file dir."
        String.concat (string Path.DirectorySeparatorChar) fileDirs.[baseDirDirsLen..]

    let private outFile (inputDir, outputDir) file =
        outputDir @+ (relativePath inputDir file)

    let private (|Content|Resource|) (f:string) =
        match (Path.GetExtension(f.ToLowerInvariant())) with
        | ".md" -> Content(Md)
        | ".fsx" -> Content(Fsx)
        | _ -> Resource

    let private input fileExists (id, od) f =
        let meta = Path.GetDirectoryName(f) @+ "meta.json"
        match f with
        | Content itype when fileExists meta ->
            PostFile(
                { File = { Input = f; Output = outFile (id, od) f @. ".html" }
                  Meta = MetaData.fromFile meta; Name = postName f
                  Type = itype })
        | Content itype -> 
            PageFile(
                { PageFile.File = {Input = f; Output = outFile (id, od) f @. ".html"}
                  Type = itype })
        | Resource -> ResourceFile({ResourceFile.File = {Input = f; Output = outFile (id, od) f}})

    let private unzip (posts, pages, resources) input =
        match input with
        | PostFile p -> (p::posts, pages, resources)
        | PageFile p -> (posts, p::pages, resources)
        | InputFile.ResourceFile r -> (posts, pages, r::resources)

    let inputFiles dirEnumerator fileExists (inputDir, outputDir) =
        dirEnumerator inputDir
        |> Seq.where (fun f -> f <> "meta.json")
        |> Seq.map (fun f -> input fileExists (inputDir, outputDir) f)
        |> Seq.fold unzip ([], [], [])

    let archive outputDir =
        outputDir @+ "blog" @+ "archive" @+ "index.html"

    let index outputDir =
        [ outputDir @+ "index.html"
          outputDir @+ "blog" @+ "index.html" ]
