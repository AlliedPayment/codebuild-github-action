using System;
using System.Collections.Generic;
using System.Text;
using Amazon.CodeBuild.Model;

namespace Allied.Codebuild
{
    class BuildRequest
    {
        public string ProjectName { get; set; }
        public string BuildspecOverride { get; set; }

        public string Arn { get; set; }

        public string Command { get; set; }
        public List<EnvironmentVariable> EnvironmentVariablesOverride { get; set; }
        public TimeSpan WaitTimeout { get; set; }
    }
}
