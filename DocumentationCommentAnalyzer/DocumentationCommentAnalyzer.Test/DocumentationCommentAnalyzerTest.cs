using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace DocumentationCommentAnalyzer.Test
{
    [TestClass]
    public class DocumentationCommentAnalyzerTest
    {
        CodeAnalysisVerifier Verifier { get; set; }
        CodeAnalysisVerifier NewVerifier() => new CodeAnalysisVerifier(new DocumentationCommentAnalyzer(), new DocumentationCommentAnalyzerCodeFixProvider());

        [TestInitialize]
        public void Initialize()
        {
            Verifier = NewVerifier();
        }
        [TestCleanup]
        public void Cleanup()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        [TestMethod]
        public void Test()
        {
            //TODO
        }

    }
}
