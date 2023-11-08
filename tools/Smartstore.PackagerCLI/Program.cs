using Smartstore.Engine.Modularity;
using Smartstore.IO;

var modulePath = args[0];
var buildPath = args[1];
var dirInfo = new DirectoryInfo(modulePath);

var lfs = new LocalFileSystem(dirInfo.ToString());
var descriptor = ModuleDescriptor.Create(new LocalDirectory("/", dirInfo,lfs), new LocalFileSystem(dirInfo.Parent.ToString()));

var pb = new Smartstore.Core.Packaging.PackageBuilder(new LocalFileSystem(dirInfo.Parent.Parent.ToString()));
var package = await pb.BuildPackageAsync(descriptor);
var fileName = package.FileName;

if (!Directory.Exists(buildPath))
{
    Directory.CreateDirectory(buildPath);
}

fileName = Path.Combine(buildPath, fileName);

await using (var stream = File.Create(fileName))
{
    await package.ArchiveStream.CopyToAsync(stream);
}

var fileInfo = new FileInfo(fileName);
Console.Write($"{fileInfo.FullName}");