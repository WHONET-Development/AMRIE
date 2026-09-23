namespace AMR_Engine
{
    /// <summary>
    /// Container for test name/result.
    /// </summary>
    public class ExpertRuleCriterion
    {

        public ExpertRuleCriterion(string testName_, string testResult_)
        {
            TestName = testName_;
            TestResult = testResult_;
        }

        public readonly string TestName;

        public readonly string TestResult;
    }
}
