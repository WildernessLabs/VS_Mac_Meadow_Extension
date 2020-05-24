﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.Projects.MSBuild;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    [ExportProjectModelExtension, AppliesTo("Meadow")]
    public class MeadowProject : DotNetProjectExtension
    {
        
        static public IDeploymentTargetsManager DeploymentTargetsManager;
        
    
        // Note: see https://github.com/mhutch/MonoDevelop.AddinMaker/blob/eff386bfcce05918dbcfe190e9c2ed8513fe92ff/MonoDevelop.AddinMaker/AddinProjectFlavor.cs#L16 for better implementation 

        // Called after the project finishes loading
        protected override void OnEndLoad()
        {
            base.OnEndLoad();

            if (DeploymentTargetsManager != null) return;  //Already loaded
       
            switch (GetRunningPlatform())
            {
                case Platform.Linux:
                    Console.WriteLine("DeploymentTargetsManagerLinux: Loading Linux");
                    DeploymentTargetsManager = new DeploymentTargetsManagerLinux();                    
                    break;
                default:
                    Console.WriteLine("DeploymentTargetsManagerLinux: Loading Mac");
                    DeploymentTargetsManager = new DeploymentTargetsManagerMac();                    
                    break;
            }
            
            var image = Xwt.Drawing.Image.FromResource("Meadow.Sdks.IdeExtensions.Vs4Mac.MeadowLogo.png");
            MonoDevelop.Ide.ImageService.AddIcon("meadow",image);


            Console.WriteLine("WLABS: OnEndLoad");

            // if the project is not a library
            // shouldn't this test if it's an executable?
            if (Project.CompileTarget != CompileTarget.Library)
            {
                Console.WriteLine("WLABS: Not a lib");

                DeploymentTargetsManager.DeviceListChanged += OnExecutionTargetsChanged;
            }
        }
        
        
        public enum Platform { Windows, Linux, Mac }
        public Platform GetRunningPlatform()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    // Well, there are chances MacOSX is reported as Unix instead of MacOSX.
                    // Instead of platform check, we'll do a feature checks (Mac specific root folders)
                    if (Directory.Exists("/Applications")
                        & Directory.Exists("/System")
                        & Directory.Exists("/Users")
                        & Directory.Exists("/Volumes"))
                        return Platform.Mac;
                    else
                        return Platform.Linux;
    
                case PlatformID.MacOSX:
                    return Platform.Mac;
    
                default:
                    return Platform.Windows;
            }
        }
        

        protected override void OnPrepareForEvaluation(MSBuildProject project)
        {
            base.OnPrepareForEvaluation(project);
        }

        public override void Dispose()
        {
            base.Dispose();
            // stop listening
            DeploymentTargetsManager.DeviceListChanged -= OnExecutionTargetsChanged;
            DeploymentTargetsManager.Dispose();
        }

        // targets changed event handler.
        private void OnExecutionTargetsChanged(object dummy)
        {
            //here if the target changes, we can look for the correct device based on the Id

            // update UI on UI thread.
         //   Runtime.RunInMainThread(() => base.OnExecutionTargetsChanged());
        }

        // probably called when the configuration changes
        protected override IEnumerable<ExecutionTarget> OnGetExecutionTargets(ConfigurationSelector configuration)
        {
            return DeploymentTargetsManager.GetTargetList();
        }

        //protected override bool OnGetSupportsFormat(Projects.MSBuild.MSBuildFileFormat format)
        //{
        //    // Q: For MHutch, how does the new AppliesTo fit into this?
        //    return format.Id == "MSBuild10" || format.Id == "MSBuild12";
        //}

        protected override bool OnGetSupportsFramework(TargetFramework framework)
        {
            // this just checks to make sure it's a "NetFramework" project, not a Silverlight project.
            Console.WriteLine($"WLABS: TargetFramework: { framework.Name }");
            return framework.Id.Identifier == TargetFrameworkMoniker.NET_4_5.Identifier;
        }

        // called by the IDE to determine whether or not the currently selected project and
        // device in the toolbar is good to go for deployment and execution.
        protected override bool OnGetCanExecute(
            ExecutionContext context,
            ConfigurationSelector configuration,
            SolutionItemRunConfiguration runConfiguration)
        {
            // find the selected solution's startup project
            if (IdeApp.Workspace.GetAllSolutions().Any((s) => s.StartupItem == this.Project))
            {
                // if the selection execution target is a meadow device, and the project is an executable.
                return context.ExecutionTarget is MeadowDeviceExecutionTarget && base.OnGetCanExecute(context, configuration, runConfiguration);
            }

            return base.OnGetCanExecute(context, configuration, runConfiguration);
        }

        protected override TargetFrameworkMoniker OnGetDefaultTargetFrameworkId()
        {
            return new TargetFrameworkMoniker("Meadow.Sdk", "0.1");
        }

        protected override TargetFrameworkMoniker OnGetDefaultTargetFrameworkForFormat(string toolsVersion)
        {
            //Keep default version invalid(1.0) or MonoDevelop will omit from serialization
            return new TargetFrameworkMoniker(".Meadow.Sdk", "1.0");
        }

        protected override ExecutionCommand OnCreateExecutionCommand(
            ConfigurationSelector configSel,
            DotNetProjectConfiguration configuration,
            ProjectRunConfiguration runConfiguration)
        {
            // build out a list of all the referenced assemblies with _full_ file paths.
            var references = Project.GetReferencedAssemblies(configSel, true).ContinueWith(t => {
                return t.Result.Select<AssemblyReference, string>((r) => {
                    if (r.FilePath.IsAbsolute)
                        return r.FilePath;
                    return Project.GetAbsoluteChildPath(r.FilePath).FullPath;
                }).ToList();
            });

            return new MeadowExecutionCommand()
            {
                OutputDirectory = configuration.OutputDirectory,
                ReferencedAssemblies = references
            };
        }
    }
}