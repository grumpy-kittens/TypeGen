﻿using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using TypeGen.Cli;
using TypeGen.Cli.Business;
using TypeGen.Cli.Models;
using TypeGen.Core;
using TypeGen.Core.Business;
using TypeGen.Core.Extensions;
using TypeGen.Core.SpecGeneration;
using TypeGen.Core.Storage;
using IGeneratorOptionsProvider = TypeGen.Cli.Business.IGeneratorOptionsProvider;
using GeneratorOptionsProvider = TypeGen.Cli.Business.GeneratorOptionsProvider;

namespace TypeGen.Cli
{
    internal class Program
    {
        private static readonly IConsoleArgsReader _consoleArgsReader;
        private static readonly ILogger _logger;
        private static readonly IFileSystem _fileSystem;
        private static readonly IConfigProvider _configProvider;
        private static readonly IGeneratorOptionsProvider _generatorOptionsProvider;
        private static readonly IProjectFileManager _projectFileManager;
        private static IAssemblyResolver _assemblyResolver;

        static Program()
        {
            _consoleArgsReader = new ConsoleArgsReader();
            _logger = new ConsoleLogger();
            _fileSystem = new FileSystem();
            _configProvider = new ConfigProvider(_fileSystem, _logger);
            _generatorOptionsProvider = new GeneratorOptionsProvider(_fileSystem, _logger);
            _projectFileManager = new ProjectFileManager(_fileSystem);
        }

        private static void Main(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0 || _consoleArgsReader.ContainsHelpOption(args) || _consoleArgsReader.ContainsAnyCommand(args) == false)
                {
                    ShowHelp();
                    return;
                }

                if (_consoleArgsReader.ContainsGetCwdCommand(args))
                {
                    string cwd = _fileSystem.GetCurrentDirectory();
                    _logger.Log($"Current working directory is: {cwd}");
                    return;
                }

                _logger.LogVerbose = _consoleArgsReader.ContainsVerboseOption(args);
                string[] configPaths = _consoleArgsReader.GetConfigPaths(args).ToArray();

                string[] projectFolders = _consoleArgsReader.ContainsProjectFolderOption(args) ?
                    _consoleArgsReader.GetProjectFolders(args).ToArray() :
                    new [] { "." };

                for (var i = 0; i < projectFolders.Length; i++)
                {
                    string projectFolder = projectFolders[i];
                    string configPath = configPaths.HasIndex(i) ? configPaths[i] : null;

                    _assemblyResolver = new AssemblyResolver(_fileSystem, _logger, projectFolder);

                    _logger.Log($"Generating files for project \"{projectFolder}\"...");
                    Generate(projectFolder, configPath);
                    _logger.Log($"Files for project \"{projectFolder}\" generated successfully.", "");
                }
            }
            catch (Exception e) when (e is CliException || e is CoreException)
            {
                _logger.Log($"APPLICATION ERROR: {e.Message}",
                    e.StackTrace);
            }
            catch (AssemblyResolutionException e)
            {
                string message = e.Message +
                                 "Consider adding any external assembly directories in the externalAssemblyPaths parameter. " +
                                 "If you're using ASP.NET Core, add your NuGet directory to externalAssemblyPaths parameter (you can use global NuGet packages directory alias: \"<global-packages>\")";
                _logger.Log(message, e.StackTrace);
            }
            catch (ReflectionTypeLoadException e)
            {
                foreach (Exception loaderException in e.LoaderExceptions)
                {
                    _logger.Log($"TYPE LOAD ERROR: {loaderException.Message}",
                        e.StackTrace);
                }
            }
            catch (Exception e)
            {
                _logger.Log($"GENERIC ERROR: {e.Message}",
                    e.StackTrace);
            }
        }

        private static void Generate(string projectFolder, string configPath)
        {
            // get config

            configPath = !string.IsNullOrEmpty(configPath)
                ? Path.Combine(projectFolder, configPath)
                : Path.Combine(projectFolder, "tgconfig.json");

            TgConfig config = _configProvider.GetConfig(configPath, projectFolder);

            // register assembly resolver

            _assemblyResolver.Directories = config.ExternalAssemblyPaths;
            _assemblyResolver.Register();

            IEnumerable<Assembly> assemblies = GetAssemblies(config.GetAssemblies()).ToArray();

            // create generator

            GeneratorOptions generatorOptions = _generatorOptionsProvider.GetGeneratorOptions(config, assemblies, projectFolder);
            generatorOptions.BaseOutputDirectory = Path.Combine(projectFolder, config.OutputPath);
            var generator = new Generator(generatorOptions, _logger);

            // generate
            
            if (config.ClearOutputDirectory == true) _fileSystem.ClearDirectory(generatorOptions.BaseOutputDirectory);
            
            IEnumerable<string> generatedFiles = Enumerable.Empty<string>();

            if (!config.GenerationSpecs.Any() || config.GenerateFromAssemblies == true)
            {
                generatedFiles = generator.Generate(assemblies);
            }

            if (config.GenerationSpecs.Any())
            {
                var typeResolver = new TypeResolver(_logger, _fileSystem, projectFolder, assemblies);
                
                generatedFiles = config.GenerationSpecs
                    .Select(name => typeResolver.Resolve(name, "GenerationSpec"))
                    .Where(t => t != null)
                    .Select(t => (GenerationSpec)Activator.CreateInstance(t))
                    .Aggregate(generatedFiles, (acc, spec) => acc.Concat(generator.Generate(spec)));
            }
            
            _logger.Log("");
            _logger.Log(generatedFiles.Select(x => $"Generated {x}").ToArray());
            _logger.Log("");
            
            if (config.AddFilesToProject ?? TgConfig.DefaultAddFilesToProject)
            {
                AddFilesToProject(projectFolder, generatedFiles);
            }

            // unregister assembly resolver

            _assemblyResolver.Unregister();
        }

        private static void AddFilesToProject(string projectFolder, IEnumerable<string> generatedFiles)
        {
            XmlDocument projectFile = _projectFileManager.ReadFromProjectFolder(projectFolder);

            foreach (string filePath in generatedFiles)
            {
                _projectFileManager.AddTsFile(projectFile, filePath);
            }

            _projectFileManager.SaveProjectFile(projectFolder, projectFile);
        }

        private static IEnumerable<Assembly> GetAssemblies(IEnumerable<string> assemblyNames)
        {
            return assemblyNames.Select(Assembly.LoadFrom);
        }

        private static void ShowHelp()
        {
            _logger.Log($"TypeGen v{AppConfig.Version}",
                "",
                "Usage: [dotnet] typegen [options] [command]",
                "",
                "Options:",
                "-h|--help               Show help information",
                "-v|--verbose            Show verbose output",
                "-p|--project-folder     Set project folder path(s)",
                "-c|--config-path        Set config path(s) to use",
                "",
                "Commands:",
                "generate     Generate TypeScript files",
                "getcwd       Get current working directory",
                "",
                "For more information please visit project's website: http://jburzynski.net/TypeGen");
        }
    }
}
