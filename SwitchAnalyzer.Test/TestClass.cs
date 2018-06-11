using System;

namespace SwitchAnalyzer.Test
{
    enum TestEnum
    {
        Case1,
        Case2,
        Case3
    }

    class TestClass : TestClass.ITestInterface
    {
        public void TestMethod1()
        {
            var s = TestEnum.Case1;

            switch (s)
            {
                case TestEnum.Case2: { break; }
            }
        }

        public TestEnum TestMethod2()
        {
            var s = TestEnum.Case1;

            switch (GetEnum(s))
            {
                case TestEnum.Case1 & TestEnum.Case1: { break; }
                case TestEnum.Case2: { break; }
                default: { break; }
            }

            return TestEnum.Case1;
        }

        public TestEnum TestMethod3()
        {
            ITestInterface s = new TestClass();

            switch (s)
            {
                case TestClass a: return TestEnum.Case1;
                case var inter: return TestEnum.Case2;
                //default: { break; }
            }
        }
        public interface ITestInterface
        {
        }

        public class NotImplementedExceptionInheritor : NotImplementedException
        {
        }

        private TestEnum GetEnum(TestEnum enumValue)
        {
            return enumValue;
        }
    }
}
