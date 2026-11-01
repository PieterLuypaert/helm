namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    // Reads agents' screens for a plan or a question, at most once per agent state. A read that failed
    // is not remembered, so the next poll tries again; a question helm cannot parse is read once more a
    // moment later, for a screen still drawing, and then not again until the agent's state changes.
    public sealed class ScreenReads
    {
        // How far back a plan's first line may sit: a long plan, or the prompt box under it, pushes it up.
        public const Int32 PlanLines = 60;
        public const Int32 QuestionLines = 40;

        // How long before an unparsed question is read once more.
        public static readonly TimeSpan Retry = TimeSpan.FromSeconds(3);

        private readonly Object _gate = new();
        private readonly Func<String, Int32, String> _read;
        private readonly Func<DateTime> _now;
        private readonly Dictionary<String, (Int64 Seq, Boolean HasPlan)> _plans = new();
        private readonly Dictionary<String, (Int64 Seq, Question Question, Boolean Retried, DateTime ReadAt)> _questions = new();

        // read gives a pane's last lines, or null when the read failed.
        public ScreenReads(Func<String, Int32, String> read, Func<DateTime> now = null)
        {
            this._read = read;
            this._now = now ?? (() => DateTime.UtcNow);
        }

        public Boolean HasPlan(String paneId, Int64 seq, Regex planLine)
        {
            lock (this._gate)
            {
                if (this._plans.TryGetValue(paneId, out var cached) && cached.Seq == seq)
                {
                    return cached.HasPlan;
                }
            }

            var screen = this._read(paneId, PlanLines);
            if (screen == null)
            {
                return false;
            }

            var hasPlan = planLine.IsMatch(screen);
            lock (this._gate)
            {
                this._plans[paneId] = (seq, hasPlan);
            }

            return hasPlan;
        }

        public Question QuestionOf(String paneId, Int64 seq)
        {
            var retried = false;
            lock (this._gate)
            {
                if (this._questions.TryGetValue(paneId, out var cached) && cached.Seq == seq)
                {
                    if (cached.Question != null || cached.Retried || this._now() - cached.ReadAt < Retry)
                    {
                        return cached.Question;
                    }

                    retried = true;
                }
            }

            var screen = this._read(paneId, QuestionLines);
            if (screen == null)
            {
                return null;
            }

            var question = Questions.Parse(screen);
            lock (this._gate)
            {
                this._questions[paneId] = (seq, question, retried, this._now());
            }

            return question;
        }

        // The next poll reads the pane's question from the screen again, such as after it was answered.
        public void ForgetQuestion(String paneId)
        {
            lock (this._gate)
            {
                this._questions.Remove(paneId);
            }
        }

        // A new plan pattern: every plan is read again.
        public void ForgetPlans()
        {
            lock (this._gate)
            {
                this._plans.Clear();
            }
        }

        // Drops what was read for panes that are gone.
        public void Keep(ISet<String> panes)
        {
            lock (this._gate)
            {
                foreach (var gone in this._plans.Keys.Where(k => !panes.Contains(k)).ToList())
                {
                    this._plans.Remove(gone);
                }

                foreach (var gone in this._questions.Keys.Where(k => !panes.Contains(k)).ToList())
                {
                    this._questions.Remove(gone);
                }
            }
        }
    }
}
