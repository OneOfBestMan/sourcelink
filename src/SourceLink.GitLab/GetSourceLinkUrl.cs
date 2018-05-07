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
        private static readonly Uri s_defaultContentUri = new Uri("https://raw.githubusercontent.com");
        private const string SourceControlName = "git";
        private const string DefaultDomain = "github.com";
        private const string NotApplicableValue = "N/A";
        private const string ContentUrlMetadataName = "ContentUrl";

        [Required]
        public ITaskItem SourceRoot { get; set; }

        /// <summary>
        /// List of additional repository hosts for which the task produces SourceLink URLs.
        /// Each item maps a domain of a repository host (stored in the item identity) to a URL of the server that provides source file content (stored in <c>ContentUrl</c> metadata).
        /// </summary>
        public ITaskItem[] Hosts { get; set; }

        [Output]
        public string SourceLinkUrl { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
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

            var mappings = GetUrlMappings().ToArray();
            var contentUri = GetMatchingContentUri(mappings, repoUri.Host);
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

            SourceLinkUrl = new Uri(contentUri, relativeUrl).ToString() + "/" + revisionId + "/*";
        }
    }
}
