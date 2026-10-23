using System;

namespace YLFramework.Services.Resource
{
    internal readonly struct ResourceBundleContext
    {
        internal string ProjectId
        {
            get;
        }
        internal string Platform
        {
            get;
        }
        internal string UnityCompatibility
        {
            get;
        }
        internal string FileRole
        {
            get;
        }
        internal string LogicalId
        {
            get;
        }

        internal ResourceBundleContext(string projectId, string platform, string unityCompatibility, string fileRole, string logicalId)
        {
            CheckText(projectId, nameof(projectId));
            CheckText(platform, nameof(platform));
            CheckText(unityCompatibility, nameof(unityCompatibility));
            CheckText(logicalId, nameof(logicalId));
            if (fileRole != "Bundle" && fileRole != "Manifest" || fileRole == "Manifest" && logicalId != "manifest")
            {
                throw new ArgumentException("Invalid resource content role/logical identity.", nameof(fileRole));
            }

            ProjectId = projectId;
            Platform = platform;
            UnityCompatibility = unityCompatibility;
            FileRole = fileRole;
            LogicalId = logicalId;
        }

        internal void RequireValid()
        {
            if (ProjectId == null)
            {
                throw new ArgumentException("An explicit resource content context is required.");
            }
        }

        private static void CheckText(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value != value.Trim())
            {
                throw new ArgumentException("An exact bounded content identity is required.", parameter);
            }

            foreach (var character in value)
            {
                if (char.IsControl(character))
                {
                    throw new ArgumentException("Control characters are not allowed.", parameter);
                }
            }
        }
    }
}
