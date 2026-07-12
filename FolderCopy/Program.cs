using FolderCopy;

// Thin entry point: all wiring lives in the app factory so Program.cs and the app-boundary tests
// build the exact same app (see FolderCopyApp and docs/guides/app-testing.md).
return await FolderCopyApp.Create().RunAsync(args);
