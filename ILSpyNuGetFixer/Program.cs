using ICSharpCode.SharpZipLib.Zip;
using System.Diagnostics;
using System.Xml.Linq;

namespace ILSpyNuGetFixer;

internal static class Program
{
	const string Url = @"https://nightly.link/icsharpcode/ILSpy/workflows/build-ilspy/master/ICSharpCode.Decompiler%20NuGet%20Package%20%28Release%29.zip";

	static async Task Main()
	{
		using HttpClient client = CreateHttpClient();
		using Stream stream = await client.GetStreamAsync(Url);

		string tempFile = Path.Combine(Environment.CurrentDirectory, Path.GetRandomFileName());
		{
			using FileStream fileStream = File.Create(tempFile);
			await stream.CopyToAsync(fileStream);
		}

		string tempDirectory = Path.Combine(Environment.CurrentDirectory, Path.GetRandomFileName());
		Directory.CreateDirectory(tempDirectory);

		ExtractZip(tempFile, tempDirectory);
		File.Delete(tempFile);
		Debug.Assert(Directory.GetFiles(tempDirectory).Length == 1);
		Debug.Assert(Directory.GetDirectories(tempDirectory).Length == 0);

		string packageFile = Directory.GetFiles(tempDirectory)[0];
		string packageFileName = Path.GetFileName(packageFile);
		string packageDirectory = Path.Combine(tempDirectory, "package");
		Directory.CreateDirectory(packageDirectory);
		ExtractZip(packageFile, packageDirectory);

		// Modify the .nuspec file
		{
			string nuspecFilePath = Directory.GetFiles(packageDirectory, "*.nuspec", SearchOption.TopDirectoryOnly).Single();
			ChangeId(nuspecFilePath);
		}

		// Repackage the .nupkg file
		{
			string newPackageFilePath = Path.Combine(AppContext.BaseDirectory, "AssetRipper." + packageFileName);
			if (File.Exists(newPackageFilePath))
			{
				File.Delete(newPackageFilePath);
			}

			CreateZip(newPackageFilePath, packageDirectory);
		}

		// Clean up the temporary directory
		Directory.Delete(tempDirectory, true);

		Console.WriteLine("Package updated successfully.");
	}

	static HttpClient CreateHttpClient()
	{
		var client = new HttpClient();
		client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("ILSpyNuGetFixer", "1.0"));
		return client;
	}

	static void ChangeId(string nuspecFilePath)
	{
		XDocument doc = XDocument.Load(nuspecFilePath);

		XElement idElement = doc.GetChild("package")
			.GetChild("metadata")
			.GetChild("id");

		idElement.Value = "AssetRipper." + idElement.Value;

		doc.Save(nuspecFilePath);
	}

	static void CreateZip(string zipFilePath, string sourceDirectory)
	{
		new FastZip().CreateZip(zipFilePath, sourceDirectory, true, null);
	}

	static void ExtractZip(string zipFilePath, string targetDirectory)
	{
		new FastZip().ExtractZip(zipFilePath, targetDirectory, null);
	}

	private static XElement GetChild(this XContainer parent, string localName)
	{
		return parent.Elements().Single(x => x.Name.LocalName == localName);
	}
}
