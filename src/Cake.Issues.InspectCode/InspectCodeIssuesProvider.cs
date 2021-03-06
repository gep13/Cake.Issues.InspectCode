﻿namespace Cake.Issues.InspectCode
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Cake.Core.Diagnostics;

    /// <summary>
    /// Provider for issues reported by JetBrains Inspect Code.
    /// </summary>
    internal class InspectCodeIssuesProvider : BaseConfigurableIssueProvider<InspectCodeIssuesSettings>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InspectCodeIssuesProvider"/> class.
        /// </summary>
        /// <param name="log">The Cake log context.</param>
        /// <param name="issueProviderSettings">Settings for the issue provider.</param>
        public InspectCodeIssuesProvider(ICakeLog log, InspectCodeIssuesSettings issueProviderSettings)
            : base(log, issueProviderSettings)
        {
        }

        /// <inheritdoc />
        public override string ProviderName => "InspectCode";

        /// <inheritdoc />
        protected override IEnumerable<IIssue> InternalReadIssues(IssueCommentFormat format)
        {
            var result = new List<IIssue>();

            var logDocument = XDocument.Parse(this.IssueProviderSettings.LogFileContent.ToStringUsingEncoding());

            var solutionPath = Path.GetDirectoryName(logDocument.Descendants("Solution").Single().Value);

            // Read all issue types.
            var issueTypes =
                logDocument.Descendants("IssueType").ToDictionary(
                    x => x.Attribute("Id")?.Value,
                    x => new IssueType
                    {
                        Severity = x.Attribute("Severity").Value,
                        WikiUrl = x.Attribute("WikiUrl")?.Value.ToUri(),
                    });

            // Loop through all issue tags.
            foreach (var issue in logDocument.Descendants("Issue"))
            {
                // Read affected project from the issue.
                if (!TryGetProject(issue, out string projectName))
                {
                    continue;
                }

                // Read affected file from the issue.
                if (!TryGetFile(issue, solutionPath, out string fileName))
                {
                    continue;
                }

                // Read affected line from the issue.
                if (!TryGetLine(issue, out int line))
                {
                    continue;
                }

                // Read rule code from the issue.
                if (!TryGetRule(issue, out string rule))
                {
                    continue;
                }

                // Read message from the issue.
                if (!TryGetMessage(issue, out string message))
                {
                    continue;
                }

                // Determine issue type properties.
                var issueType = issueTypes[rule];
                var severity = issueType.Severity.ToLowerInvariant();
                var ruleUrl = issueType.WikiUrl;

                // Build issue.
                result.Add(
                    IssueBuilder
                        .NewIssue(message, this)
                        .InProjectOfName(projectName)
                        .InFile(fileName, line)
                        .WithPriority(GetPriority(severity))
                        .OfRule(rule, ruleUrl)
                        .Create());
            }

            return result;
        }

        /// <summary>
        /// Determines the project for a issue logged in a Inspect Code log.
        /// </summary>
        /// <param name="issue">Issue element from Inspect Code log.</param>
        /// <param name="project">Returns project.</param>
        /// <returns>True if the project could be parsed.</returns>
        private static bool TryGetProject(
            XElement issue,
            out string project)
        {
            project = string.Empty;

            var projectNode = issue.Ancestors("Project").FirstOrDefault();
            if (projectNode == null)
            {
                return false;
            }

            var projectAttr = projectNode.Attribute("Name");
            if (projectAttr == null)
            {
                return false;
            }

            project = projectAttr.Value;
            if (string.IsNullOrWhiteSpace(project))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reads the affected file path from an issue logged in a Inspect Code log.
        /// </summary>
        /// <param name="issue">Issue element from Inspect Code log.</param>
        /// <param name="solutionPath">Path to the solution file.</param>
        /// <param name="fileName">Returns the full path to the affected file.</param>
        /// <returns>True if the file path could be parsed.</returns>
        private static bool TryGetFile(XElement issue, string solutionPath, out string fileName)
        {
            fileName = string.Empty;

            var fileAttr = issue.Attribute("File");
            if (fileAttr == null)
            {
                return false;
            }

            fileName = fileAttr.Value;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            // Combine with path to the solution file.
            fileName = Path.Combine(solutionPath, fileName);

            return true;
        }

        /// <summary>
        /// Reads the affected line from an issue logged in a Inspect Code log.
        /// </summary>
        /// <param name="issue">Issue element from Inspect Code log.</param>
        /// <param name="line">Returns line.</param>
        /// <returns>True if the line could be parsed.</returns>
        private static bool TryGetLine(XElement issue, out int line)
        {
            line = -1;

            var lineAttr = issue.Attribute("Line");

            var lineValue = lineAttr?.Value;
            if (string.IsNullOrWhiteSpace(lineValue))
            {
                return false;
            }

            line = int.Parse(lineValue, CultureInfo.InvariantCulture);

            return true;
        }

        /// <summary>
        /// Reads the rule code from an issue logged in a Inspect Code log.
        /// </summary>
        /// <param name="issue">Issue element from Inspect Code log.</param>
        /// <param name="rule">Returns the code of the rule.</param>
        /// <returns>True if the rule code could be parsed.</returns>
        private static bool TryGetRule(XElement issue, out string rule)
        {
            rule = string.Empty;

            var codeAttr = issue.Attribute("TypeId");
            if (codeAttr == null)
            {
                return false;
            }

            rule = codeAttr.Value;
            if (string.IsNullOrWhiteSpace(rule))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reads the message from an issue logged in a Inspect Code log.
        /// </summary>
        /// <param name="issue">Issue element from Inspect Code log.</param>
        /// <param name="message">Returns the message of the issue.</param>
        /// <returns>True if the message could be parsed.</returns>
        private static bool TryGetMessage(XElement issue, out string message)
        {
            message = string.Empty;

            var messageAttr = issue.Attribute("Message");
            if (messageAttr == null)
            {
                return false;
            }

            message = messageAttr.Value;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converts the severity level to a priority.
        /// </summary>
        /// <param name="severity">Severity level as reported by InspectCode.</param>
        /// <returns>Priority.</returns>
        private static IssuePriority GetPriority(string severity)
        {
            switch (severity.ToLowerInvariant())
            {
                case "hint":
                    return IssuePriority.Hint;

                case "suggestion":
                    return IssuePriority.Suggestion;

                case "warning":
                    return IssuePriority.Warning;

                case "error":
                    return IssuePriority.Error;

                default:
                    return IssuePriority.Undefined;
            }
        }

        /// <summary>
        /// Description of an issue type.
        /// </summary>
        private class IssueType
        {
            /// <summary>
            /// Gets or sets the severity of this issue type.
            /// </summary>
            public string Severity { get; set; }

            /// <summary>
            /// Gets or sets the URL to the page containing documentation about this issue type.
            /// </summary>
            public Uri WikiUrl { get; set; }
        }
    }
}
