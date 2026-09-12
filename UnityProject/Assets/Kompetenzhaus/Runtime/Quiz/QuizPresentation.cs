using Kompetenzhaus.Content;

namespace Kompetenzhaus.Quiz
{
    // Display-only wording for three inherited questions whose historical text names
    // the superseded framework. Source questions, options and answer indices stay intact.
    public static class QuizPresentation
    {
        public static string Prompt(QuizQuestion question, string language)
        {
            var english = language == "en";
            return question?.id switch
            {
                "06SM200-400:q2" => english
                    ? "A language model gives a case vignette the tentative diagnosis \"generalised anxiety disorder\", cites three plausible arguments and appears very convincing. Which professional check best tests the diagnostic claim?"
                    : "Ein Sprachmodell liefert zu einer Fallvignette die Verdachtsdiagnose «generalisierte Angststörung», nennt drei plausible Argumente und wirkt sehr überzeugend. Welche fachliche Prüfung überprüft die diagnostische Aussage am wirksamsten?",
                "06SM200-103:q2" => english
                    ? "A student asks an LLM which test compares two group means; it confidently recommends an independent-samples t-test. The data are strongly right-skewed, variances unequal, and n small. What is the appropriate response when checking this recommendation?"
                    : "Eine Studentin fragt ein LLM, welcher Test zwei Gruppenmittelwerte vergleicht; es empfiehlt selbstsicher einen unabhängigen t-Test. Die Daten sind stark rechtsschief, die Varianzen ungleich und n klein. Wie sollte diese Empfehlung fachlich geprüft werden?",
                "06SM200-502:q3" => english
                    ? "A prevention project plans to offer stress management in a socioeconomically disadvantaged community exclusively via an AI-supported smartphone app. Which objection concerning equitable access and the community's circumstances carries the most weight?"
                    : "Ein Präventionsprojekt will Stressbewältigung in einer sozioökonomisch benachteiligten Gemeinde ausschliesslich über eine KI-gestützte Smartphone-App anbieten. Welcher Einwand zu Zugangsgerechtigkeit und den Lebensbedingungen der Gemeinde wiegt am schwersten?",
                _ => question?.prompt?.Get(language) ?? ""
            };
        }

        public static string Explanation(QuizQuestion question, string language)
        {
            var english = language == "en";
            return question?.id switch
            {
                "06SM200-400:q2" => english
                    ? "The diagnostic claim needs verification against the criteria standard: a systematic comparison with the full diagnostic criteria and differential diagnoses can expose faulty conclusions. Persuasiveness, model consensus and self-assessment do not establish accuracy, because language models can be plausibly and consistently wrong."
                    : "Die diagnostische Aussage braucht eine fachliche Prüfung am Kriterienstandard: Der systematische Abgleich mit den vollständigen Diagnosekriterien und Differenzialdiagnosen kann Fehlschlüsse aufdecken. Überzeugungskraft, Modellkonsens und Selbsteinschätzung sind keine Gütebelege, weil Sprachmodelle plausibel und konsistent falschliegen können.",
                "06SM200-103:q2" => english
                    ? "Check the AI suggestion against the statistical assumptions, not against how confidently it is stated. With strong skew, unequal variances and small n, the procedure and its assumptions need careful review; consider an appropriate robust or nonparametric alternative. Simply switching to a standard ANOVA does not resolve the issue: with two groups, it corresponds to the standard equal-variance t-test and shares its assumptions."
                    : "Prüfe den KI-Vorschlag gegen die statistischen Annahmen, nicht gegen sein selbstsicheres Auftreten. Bei starker Schiefe, ungleichen Varianzen und kleinem n müssen Verfahren und Voraussetzungen sorgfältig geprüft werden; eine geeignete robuste oder nichtparametrische Alternative ist zu erwägen. Ein blosser Wechsel zur Standard-ANOVA löst das Problem nicht: Bei zwei Gruppen entspricht sie dem Standard-t-Test mit gleichen Varianzen und teilt dessen Annahmen.",
                "06SM200-502:q3" => english
                    ? "Digital offerings require access, devices and digital health literacy. When these are unevenly distributed, already advantaged groups may benefit most and existing health inequalities may widen. Review access and the community's circumstances. The server-location option neither identifies this central problem nor supports its sweeping legal claim."
                    : "Digitale Angebote setzen Zugang, Geräte und digitale Gesundheitskompetenz voraus. Sind diese ungleich verteilt, können vor allem ohnehin begünstigte Gruppen profitieren und bestehende gesundheitliche Ungleichheiten wachsen. Zugang und Lebensbedingungen der Gemeinde müssen deshalb geprüft werden. Der Serverstandort-Distraktor benennt dieses Kernproblem nicht und belegt seine pauschale Rechtsbehauptung nicht.",
                _ => question?.explanation?.Get(language) ?? ""
            };
        }
    }
}
