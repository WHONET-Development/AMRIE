namespace AMRIE.Library
{
    /// <summary>
    /// Container for test name/result.
    /// </summary>
    public class ExpertRuleCriterion(string testName_, string testResult_)
    {
        public readonly string TestName = testName_;

        public readonly string TestResult = testResult_;
    }
}
