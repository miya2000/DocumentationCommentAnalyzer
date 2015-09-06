using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static DocumentationCommentAnalyzer.DocumentationCommentAnalyzerCodeFixProvider;

namespace DocumentationCommentAnalyzer.Test
{
    [TestClass]
    public class RpairXmlTest
    {
        [TestMethod]
        public void FixXmlTest()
        {
            RpairXml(@"<a>a <<>> b</a>").Is(@"<a>a &lt;&lt;>> b</a>");
            RpairXml(@"<a>a && b</a>").Is(@"<a>a &amp;&amp; b</a>");
            RpairXml(@"<a> </x> </a>").Is(@"<a> <x></x> </a>");
            RpairXml(@"<a> <x> </a>").Is(@"<a> <x> </x></a>");
            RpairXml(@"<a> <date>2015-01-01</date2> </a>").Is(@"<a> <date>2015-01-01<date2></date2> </date></a>");
            RpairXml(@"<a> <a> <a> ").Is(@"<a> <a> <a> </a></a></a>");
            RpairXml(@"<a> </a> </a> ").Is(@"<a> </a> <a></a> ");
            RpairXml(@"<a> </a> <a> ").Is(@"<a> </a> <a> </a>");
            RpairXml(@"<a> <a> <a> </a> </b> <a /> </> </a> ").Is(@"<a> <a> <a> </a> <b></b> <a /> &lt;/> </a> </a>");

            // Not Support Currently.
            RpairXml(@"<a aaa=""></a><b bbb="""" />").Is(@"<a aaa=""></a><b bbb="""" />");
        }

        [TestMethod]
        public void GetPositionTest1()
        {
            var str = "123\n456\r\n789";

            GetPosition(str, 1, 1).Is(0);
            GetPosition(str, 1, 2).Is(1);
            GetPosition(str, 1, 3).Is(2);
            GetPosition(str, 2, 1).Is(4);
            GetPosition(str, 2, 2).Is(5);
            GetPosition(str, 2, 3).Is(6);
            GetPosition(str, 3, 1).Is(9);
            GetPosition(str, 3, 2).Is(10);
            GetPosition(str, 3, 3).Is(11);
        }
        [TestMethod]
        public void GetPositionTest2()
        {
            var str = "あいう\nかきく\r\nさしす";

            GetPosition(str, 1, 1).Is(0);
            GetPosition(str, 1, 2).Is(1);
            GetPosition(str, 1, 3).Is(2);
            GetPosition(str, 2, 1).Is(4);
            GetPosition(str, 2, 2).Is(5);
            GetPosition(str, 2, 3).Is(6);
            GetPosition(str, 3, 1).Is(9);
            GetPosition(str, 3, 2).Is(10);
            GetPosition(str, 3, 3).Is(11);
        }
    }
}
