using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Content;

namespace Kompetenzhaus.Quiz
{
    public enum QuizPhase { Idle, Question, Feedback, Complete }

    // Plain C# state: no renderer, timer, scene callback or animation can skip explanatory feedback.
    public sealed class QuizSession
    {
        public event Action Changed;
        public event Action Completed;
        public QuizPhase Phase { get; private set; }
        public QuizQuestion CurrentQuestion => currentIndex >= 0 ? questions[currentIndex] : null;
        public IReadOnlyList<int> OptionOrder => optionOrder;
        public bool LastAnswerCorrect { get; private set; }
        public int SelectedOptionIndex { get; private set; } = -1;
        public int MasteredCount => mastered.Count;
        public int QuestionCount => questions.Length;
        public int Mistakes { get; private set; }
        public int Attempts { get; private set; }
        private QuizQuestion[] questions = Array.Empty<QuizQuestion>();
        private readonly Queue<int> pending = new Queue<int>();
        private readonly HashSet<int> mastered = new HashSet<int>();
        private int currentIndex = -1;
        private int[] optionOrder = Array.Empty<int>();
        private readonly Random random;
        private bool shuffle;

        public QuizSession(int? seed = null) { random = seed.HasValue ? new Random(seed.Value) : new Random(); }

        public void Start(QuizQuestion[] sourceQuestions, bool shuffleOptions = true)
        {
            if (sourceQuestions == null || sourceQuestions.Length == 0) throw new ArgumentException("A quiz needs questions.");
            ContentCatalog.ValidateQuestions(sourceQuestions, "session");
            questions = sourceQuestions.ToArray();
            pending.Clear();
            mastered.Clear();
            for (var i = 0; i < questions.Length; i++) pending.Enqueue(i);
            shuffle = shuffleOptions;
            Mistakes = Attempts = 0;
            ShowNext();
        }

        public bool Answer(int displayedOptionIndex)
        {
            if (Phase != QuizPhase.Question || displayedOptionIndex < 0 || displayedOptionIndex >= optionOrder.Length) return false;
            SelectedOptionIndex = optionOrder[displayedOptionIndex];
            LastAnswerCorrect = SelectedOptionIndex == CurrentQuestion.correctIndex;
            Attempts++;
            if (LastAnswerCorrect) mastered.Add(currentIndex);
            else Mistakes++;
            Phase = QuizPhase.Feedback;
            Changed?.Invoke();
            return true;
        }

        public bool Continue()
        {
            if (Phase != QuizPhase.Feedback) return false;
            if (!LastAnswerCorrect && !mastered.Contains(currentIndex))
            {
                // If only the missed item remains, put a solved item between attempts.
                // Single-question banks have no possible spacer; their explanation still requires Continue.
                if (pending.Count == 0 && mastered.Count > 0) pending.Enqueue(mastered.OrderBy(i => i).First());
                pending.Enqueue(currentIndex);
            }
            if (mastered.Count == questions.Length)
            {
                Phase = QuizPhase.Complete;
                Changed?.Invoke();
                Completed?.Invoke();
            }
            else ShowNext();
            return true;
        }

        public void Cancel()
        {
            Phase = QuizPhase.Idle;
            currentIndex = -1;
            pending.Clear();
            Changed?.Invoke();
        }

        private void ShowNext()
        {
            if (pending.Count == 0) throw new InvalidOperationException("Quiz queue exhausted before mastery.");
            currentIndex = pending.Dequeue();
            optionOrder = Enumerable.Range(0, CurrentQuestion.options.Length).ToArray();
            if (shuffle)
                for (var i = optionOrder.Length - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (optionOrder[i], optionOrder[j]) = (optionOrder[j], optionOrder[i]);
                }
            SelectedOptionIndex = -1;
            Phase = QuizPhase.Question;
            Changed?.Invoke();
        }
    }
}
