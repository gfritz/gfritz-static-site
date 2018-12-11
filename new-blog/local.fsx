#r "paket: groupref localhosting //"
#load ".fake/local.fsx/intellisense.fsx"

#if !FAKE
  #r ".fake/blog.fsx/NETStandard.Library.NETFramework/build/netstandard2.0/ref/netstandard.dll"
#endif

#nowarn "52"