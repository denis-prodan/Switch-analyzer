# Switch-analyzer
C# analyzer for non-exhaustive cases in switch with enums.
Verifies that switch statement checks all existing enum values if case if there is no **default** branch or it throws *NotImplementedException* (or one of its inheritors).

For code:
```C#
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
        switch (TestEnum.Case1)
        {
            case TestEnum.Case1: return TestEnum.Case2;
            case TestEnum.Case2: return TestEnum.Case1;
            default: throw new NotImplementedException();
        }
    }
}
```
    
You will get warning, because **TestEnum.Case3** is not covered in this switch statement.
At this moment analyzer should support common cases:
* Bitwise operators (& and |).
* Parentnesis.
* Function call as switch argument.

You can find more cases in unit test
