using System.Collections.Generic;

namespace Nexus.Planet
{
    /// <summary>
    /// Collects human readable problems found in planet data. Validation never throws: the
    /// loader degrades to safe values and reports every problem here so tests and the editor
    /// report can show what was wrong with the authored data.
    /// </summary>
    public sealed class PlanetValidationReport
    {
        private readonly List<string> _issues = new List<string>();

        public bool IsValid
        {
            get { return _issues.Count == 0; }
        }

        public int IssueCount
        {
            get { return _issues.Count; }
        }

        public IReadOnlyList<string> Issues
        {
            get { return _issues; }
        }

        public void Add(string issue)
        {
            if (string.IsNullOrEmpty(issue))
            {
                return;
            }

            _issues.Add(issue);
        }

        public void Add(string context, string issue)
        {
            Add(string.IsNullOrEmpty(context) ? issue : context + ": " + issue);
        }

        public string Summary
        {
            get
            {
                if (_issues.Count == 0)
                {
                    return "planet data is valid";
                }

                return _issues.Count + " issue(s): " + string.Join("; ", _issues.ToArray());
            }
        }

        public override string ToString()
        {
            return Summary;
        }
    }
}
