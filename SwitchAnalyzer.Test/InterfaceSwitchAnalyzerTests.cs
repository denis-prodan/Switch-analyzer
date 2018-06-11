using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace SwitchAnalyzer.Test
{
    [TestClass]
    public class InterfaceSwitchAnalyzerTests : CodeFixVerifier
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

    public interface ITestInterface
    {
    }

        public class TestClass: ITestInterface
        {
            public TestEnum TestMethod()
            {
                var testValue = TestEnum.Case1;";

        private readonly string codeEnd = @"
            }

            public class OneMoreInheritor : ITestInterface
            {
            }
            private TestEnum GetEnum(TestEnum enumValue)
            {
                return enumValue;
            }
        }
    }";

        [TestMethod]
        public void SimpleValid()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a: return TestEnum.Case1;
                case OneMoreInheritor a: return TestEnum.Case2;
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
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("OneMoreInheritor"));
        }

        [TestMethod]
        public void CheckWithThrowInBlock()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a: return TestEnum.Case1;
                default: default:{
                        var s = GetEnum(testValue);
                        throw new NotImplementedException();
                        }
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("OneMoreInheritor"));
        }

        [TestMethod]
        public void NoChecksWithoutThrowInDefault()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a: return TestEnum.Case1;
                default: default:{
                        var s = GetEnum(testValue);
                        break;
                        }
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void MultipleValuesReturnedInDiagnostic()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                default: default:{
                        var s = GetEnum(testValue);
                        throw new NotImplementedException();
                        }
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("OneMoreInheritor", "TestClass"));
        }

        [TestMethod]
        public void ArgumentAsTypeConversionValid()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            switch (new TestClass() as ITestInterface)
            {
                case TestClass a: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("OneMoreInheritor"));
        }

        private DiagnosticResult GetDiagnostic(params string[] expectedTypes)
        {
            return new DiagnosticResult
            {
                Id = "SA002",
                Message = String.Format("Switch case should check interface implementation of type(s): {0}", string.Join(", ", expectedTypes)),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 30, 13)
                    }
            };
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SwitchAnalyzer();
        }
    }
}
