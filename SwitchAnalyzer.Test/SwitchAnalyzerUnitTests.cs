using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;

namespace SwitchAnalyzer.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        private readonly string codeStart = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
    
    enum TestEnum
    {
        Case1,
        Case2,
        Case3
    }

        class TestClass
        {
            public TestEnum TestMethod()
            {
                var testValue = TestEnum.Case1;";

        private readonly string codeEnd =
        @"
            }

            public class NotImplementedExceptionInheritor : NotImplementedException
            {
            }
            private TestEnum GetEnum(TestEnum enumValue)
            {
                return enumValue;
            }
        }
    }";

        //No diagnostics expected to show up
        [TestMethod]
        public void EmptyValid()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SimpleValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1: return TestEnum.Case1;
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SimpleInvalid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1"));
        }

        [TestMethod]
        public void ChecksWithNotImplementedExceptionInheritor()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedExceptionInheritor();
            }";

            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1"));
        }

        [TestMethod]
        public void NoChecksWithoutNotImplementedExceptionInDefault()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case2: {break;}
                case TestEnum.Case3: {break;}
                default: throw new ArgumentException();
            }
            return TestEnum.Case2;";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void MultipleValuesSpecified()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1", "TestEnum.Case3"));
        }

        [TestMethod]
        public void ArgumentAsMethodCallValid()
        {
            var switchStatement = @"
            switch (GetEnum(testValue))
            {
                case TestEnum.Case1: return TestEnum.Case1;
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void BItwiseOrValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1 | TestEnum.Case2: return TestEnum.Case1;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void BitwiseAndInvalid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1 & TestEnum.Case2: return TestEnum.Case1;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1, TestEnum.Case2"));
        }

        [TestMethod]
        public void BitwiseAndSameResultValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1 & TestEnum.Case1: return TestEnum.Case1;
                case TestEnum.Case2: return TestEnum.Case3;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ComplexBitwiseCaseValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case (TestEnum.Case1 & TestEnum.Case1) | (TestEnum.Case2 | TestEnum.Case3): return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ComplexBitwiseCaseInvalid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case (TestEnum.Case1 & TestEnum.Case1) | (TestEnum.Case2 & TestEnum.Case3): return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case2", "TestEnum.Case3"));
        }

        private DiagnosticResult GetDiagnostic(params string[] expectedEnums)
        {
            return new DiagnosticResult
            {
                Id = "SwitchAnalyzer",
                Message = String.Format("Switch case should check enum value(s): {0}", string.Join(", ", expectedEnums)),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 25, 13)
                    }
            };
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SwitchAnalyzerAnalyzer();
        }
    }
}
