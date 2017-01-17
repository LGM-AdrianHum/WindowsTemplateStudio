using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.Templates.Core;
using Microsoft.Templates.Core.Locations;
using Xunit;

namespace Microsoft.Templates.Test
{
    public class GenerationTests : IClassFixture<GenerationTestsFixture>
    {
        private GenerationTestsFixture _fixture;
        private const string Platform = "x86";
        private const string Configuration = "Debug";

        public GenerationTests(GenerationTestsFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory, MemberData("GetAppTemplates", GenerationTestsFixture.TemplatePath), Trait("Type", "AppGeneration")]
        public void GenerateAndBuildAppFromTemplate(string templateName)
        {
            //Set up test repos
            var repos = new TemplatesRepository(new TestTemplatesLocation());
            repos.Sync();

            //Generate app
            var outputPath = GenerationTestsFixture.TestAppsPath + templateName;
            GenerateApp(templateName, repos, outputPath);

            //Build solution
            int exitCode = BuildSolution(templateName, outputPath);

            //Assert
            Assert.True(exitCode.Equals(0), string.Format("Solution {0} was not built successfully.", templateName));
        }


        [Theory, MemberData("GetPageTemplates", GenerationTestsFixture.TemplatePath), Trait("Type", "PageGeneration")]
        public void GenerateAndBuildPageFromTemplate(string templateName,  string appTemplateName)
        {
            //Set up test repos
            var repos = new TemplatesRepository(new TestTemplatesLocation());
            repos.Sync();

            //Generate app
            var appOutputPath = GenerationTestsFixture.TestPagesPath + appTemplateName;
            GenerateApp(appTemplateName, repos, appOutputPath);

            //Generate page
            var pageOutputPath = appOutputPath + "\\" + appTemplateName;
            var page = GeneratePage(templateName, repos, pageOutputPath);

            //Add file to proj
            AddToProject(appTemplateName, pageOutputPath, page);

            //Build solution
            int exitCode = BuildSolution(appTemplateName, appOutputPath);

            //Assert
            Assert.True(exitCode.Equals(0), string.Format("Solution {0} with page {1} was not built successfully.", appTemplateName, templateName));
            
        }

        public static IEnumerable<object[]> GetPageTemplates(string path)
        {
            foreach (var folder in Directory.EnumerateDirectories(path))
            {
                if (folder.EndsWith("Page"))
                {
                    var templateName = Path.GetFileName(folder);
                    yield return new object[] { templateName, templateName.Replace("Page", "App") };
                }
            }
        }

        public static IEnumerable<object[]> GetAppTemplates(string path)
        {
            foreach (var folder in Directory.EnumerateDirectories(path))
            {
                if (folder.EndsWith("App"))
                {
                    yield return new object[] { Path.GetFileName(folder) };
                }
            }
        }

        private static void AddToProject(string projectName, string projectPath, TemplateCreationResult page)
        {
            var projectFile = Path.GetFullPath(projectPath + @"\\" + projectName + ".csproj");
            var msbuildProj = MsBuildProject.Load(projectFile);
            if (msbuildProj != null)
            {
                foreach (var output in page.PrimaryOutputs)
                {
                    if (!string.IsNullOrWhiteSpace(output.Path))
                    {
                        var itemPath = Path.GetFullPath(Path.Combine(projectPath, output.Path));
                        msbuildProj.AddItem(itemPath);
                    }
                }

                msbuildProj.Save();
            }
        }

        private static TemplateCreationResult GenerateApp(string templateName, TemplatesRepository repos, string outputPath)
        {
            var template = repos.GetAll().First(t => t.Name == templateName);
            return TemplateCreator.InstantiateAsync(template, templateName, null, outputPath, new Dictionary<string, string>(), true).Result;
        }

        private static TemplateCreationResult GeneratePage(string templateName, TemplatesRepository repos, string pageOutputPath)
        {
            var pageTemplate = repos.GetAll().First(t => t.Name == templateName);
            return TemplateCreator.InstantiateAsync(pageTemplate, templateName, null, pageOutputPath, new Dictionary<string, string>(), true).Result;
        }

        private static int BuildSolution(string solutionName, string outputPath)
        {
            //Build
            var solutionFile = Path.GetFullPath(outputPath + @"\" + solutionName + ".sln");
            var startInfo = new ProcessStartInfo(GetPath("RestoreAndBuild.bat"))
            {
                Arguments = $"\"{solutionFile}\" {Platform} {Configuration}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = false,
                WorkingDirectory = outputPath
            };

            var process = Process.Start(startInfo);
            Debug.Print(process.StandardOutput.ReadToEnd());
            process.WaitForExit();

            return process.ExitCode;
        }

        internal static string GetPath(string fileName)
        {
            string path = Path.Combine(new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName, fileName);
            if (!File.Exists(path))
            {
                path = Path.GetFullPath($@".\{fileName}");
                if (!File.Exists(path))
                {
                    throw new ApplicationException($"Can not find {fileName}");
                }
            }
            return path;
        }
    }
}
