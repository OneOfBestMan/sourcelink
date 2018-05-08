// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.GitLab
{
    /// <summary>
    /// The task calculates SourceLink URL for a given GitLab SourceRoot.
    /// </summary>
    public sealed class GetSourceLinkUrl : Task
    {
        // http://gitlab-ee.hxakzvpf0otezeojz3wqhme5wg.cx.internal.cloudapp.net/Administrator/test1.git
        private static readonly Uri s_defaultContentUri = new Uri("https://raw.githubusercontent.com");

        private const string GitLabExternalUrlItemGroupName = "SourceLinkGitLabExternalUrl";
        private const string PackageDisplayName = "Microsoft.SourceLink.GitLab";
        private const string GitLabName = "GitLab";

        private const string SourceControlName = "git";
        private const string NotApplicableValue = "N/A";

        [Required]
        public ITaskItem SourceRoot { get; set; }

        /// <summary>
        /// ExternalUrls lists base URLs of git repositories hosted by GitLab.
        /// Multiple values can be specified if you have mutliple GitLab instances with different external URL.
        /// The task will use this list to match URLs stored in the git repository (remote origin and submodule URLs).
        /// 
        /// See https://docs.gitlab.com/omnibus/settings/configuration.html#configuring-the-external-url-for-gitlab
        /// </summary>
        public string ExternalUrls { get; set; }

        [Output]
        public string SourceLinkUrl { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            var externalUris = GetExternalUris().ToArray();
            if (externalUris.Length == 0)
            {
                Log.LogError(Resources.PackageNeedsAtLeastOneServerUrl, GitLabExternalUrlItemGroupName, PackageDisplayName, GitLabName);
                return;
            }

            // skip SourceRoot that already has SourceLinkUrl set, or its SourceControl is not "git":
            if (!string.IsNullOrEmpty(SourceRoot.GetMetadata(Names.SourceRoot.SourceLinkUrl)) ||
                !string.Equals(SourceRoot.GetMetadata(Names.SourceRoot.SourceControl), SourceControlName, StringComparison.OrdinalIgnoreCase))
            {
                SourceLinkUrl = NotApplicableValue;
                return;
            }

            var repoUrl = SourceRoot.GetMetadata(Names.SourceRoot.RepositoryUrl);
            if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out var repoUri))
            {
                Log.LogError(Resources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot.ItemSpec, repoUrl);
                return;
            }

            var contentUri = MapRepositoryUrl(repoUri, externalUris);
            if (contentUri == null)
            {
                SourceLinkUrl = NotApplicableValue;
                return;
            }

            bool IsHexDigit(char c)
                => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';

            string revisionId = SourceRoot.GetMetadata(Names.SourceRoot.RevisionId);
            if (revisionId == null || revisionId.Length != 40 || !revisionId.All(IsHexDigit))
            {
                Log.LogError(Resources.ValueOfWithIdentityIsNotValidCommitHash, Names.SourceRoot.RevisionIdFullName, SourceRoot.ItemSpec, revisionId);
                return;
            }

            var relativeUrl = repoUri.LocalPath.TrimEnd('/');

            // The URL may or may not end with '.git', but raw.githubusercontent.com does not accept '.git' suffix:
            const string gitUrlSuffix = ".git";
            if (relativeUrl.EndsWith(gitUrlSuffix))
            {
                relativeUrl = relativeUrl.Substring(0, relativeUrl.Length - gitUrlSuffix.Length);
            }

            SourceLinkUrl = new Uri(contentUri, relativeUrl).ToString() + "/raw/" + revisionId + "/*";
        }

        private IEnumerable<Uri> GetExternalUris()
        {
            var urls = ExternalUrls?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            foreach (string url in urls)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    yield return uri;
                }
                else
                {
                    Log.LogWarning(Resources.SpecifiesAnInvalidUrl, GitLabExternalUrlItemGroupName, url);
                }
            }
        }

        private Uri MapRepositoryUrl(Uri repoUri, Uri[] externalUris)
        {
            // TODO:
            return null;
        }
    }
}
