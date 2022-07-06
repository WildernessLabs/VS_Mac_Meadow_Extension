﻿using System;
using System.Collections.Generic;
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
        const string MeadowSdkProject = "Meadow.Sdk/1.1.0";

        // Note: see https://github.com/mhutch/MonoDevelop.AddinMaker/blob/eff386bfcce05918dbcfe190e9c2ed8513fe92ff/MonoDevelop.AddinMaker/AddinProjectFlavor.cs#L16 for better implementation 

        // Called after the project finishes loading
        protected override void OnEndLoad()
        {
            base.OnEndLoad();

            Console.WriteLine("WLABS: OnEndLoad");

            // Check that the project is a library, so we can start listening for attached/detached devices.
            if (Project.CompileTarget == CompileTarget.Library)
            {
                Console.WriteLine("WLABS: IS a lib");

                DeploymentTargetsManager.DeviceListChanged += OnExecutionTargetsChanged;
                DeploymentTargetsManager.StartPollingForDevices();
            }
        }

        protected override void OnPrepareForEvaluation(MSBuildProject project)
        {
            base.OnPrepareForEvaluation(project);
        }

        public override void Dispose()
        {
            base.Dispose();
            // stop listening for for attached/detached devices.
            DeploymentTargetsManager.DeviceListChanged -= OnExecutionTargetsChanged;
            DeploymentTargetsManager.StopPollingForDevices();
        }

        // targets changed event handler.
        private void OnExecutionTargetsChanged(object dummy)
        {
            //here if the target changes, we can look for the correct device based on the Id

            // update UI on UI thread.
            Runtime.RunInMainThread(() => base.OnExecutionTargetsChanged());
        }

        // probably called when the configuration changes
        protected override IEnumerable<ExecutionTarget> OnGetExecutionTargets(ConfigurationSelector configuration)
        {
            return DeploymentTargetsManager.Targets;
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
            var isMeadowMeadowDeviceExecutionTarget = context.ExecutionTarget is MeadowDeviceExecutionTarget;
            // find the selected solution's startup project
            if (IdeApp.Workspace.GetAllSolutions().Any((s) => s.StartupItem == this.Project)
                && isMeadowMeadowDeviceExecutionTarget
                && IsMeadowSdkProject())
            {
                return true;
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

        protected override ProjectFeatures OnGetSupportedFeatures ()
        {
            var features = base.OnGetSupportedFeatures ();
            if (IsMeadowSdkProject ())
                features |= ProjectFeatures.Execute;
            return features;
        }

        private bool IsMeadowSdkProject ()
        {
            return this.Project.MSBuildProject.GetReferencedSDKs ().Any ((s) => s == MeadowSdkProject);
        }
    }
}