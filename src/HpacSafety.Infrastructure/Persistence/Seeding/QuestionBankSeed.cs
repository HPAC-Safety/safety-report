using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
/// The question set HPAC has been collecting through Typeform, as data. A clean
/// database asks exactly this, so a report filed on day one is comparable with
/// the ones already in the archive.
/// </summary>
/// <remarks>
/// <para>
/// This is a transcription of <c>docs/form-spec.md</c>, and
/// <c>QuestionBankSeedTests</c> reads that file and fails if the two ever drift
/// apart. Regenerate the spec with <c>tools/extract-typeform.py</c>; never edit
/// it by hand, and update this file to match when it changes.
/// </para>
/// <para>
/// Every question here is ordinary data that an administrator may reword,
/// reorder, retype, or delete — except <c>consent_publish</c>, which gates
/// publication and cannot be removed. See ADR-0016.
/// </para>
/// <para>
/// The French wording is machine-translated and carries
/// <c>is_machine_translated = true</c>: it is good enough to render, and it has
/// not been reviewed by a person. See ADR-0020.
/// </para>
/// </remarks>
public static class QuestionBankSeed
{
    /// <summary>
    /// The instant every seeded row is stamped with. Fixed, so that applying
    /// the migration twice on two databases produces identical rows.
    /// </summary>
    public static readonly DateTimeOffset SeededAt = new(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The seeded form, in display order.</summary>
    public static IReadOnlyList<SeededQuestion> Questions { get; } = Build();

    private static IReadOnlyList<SeededQuestion> Build() =>
    [
        Statement(
            "intro",
            "Tell us as much as you can about the occurrence",
            "Dites-nous-en le plus possible sur l'événement",
            "We will first ask you 15 short questions about the pilot, location, aircraft, injury, and damage.\nWe will then ask you to provide a detailed description of the occurrence, and the corrective actions that were taken after the occurrence (along with your recommendations).\nYou can type in your description or prepare it in advance and copy-paste it. You can leave the form and come back to it. Your answers are saved in your browser for 15 days or until you submit the form.",
            "Nous vous poserons d'abord 15 courtes questions sur le pilote, le lieu, l'aéronef, les blessures et les dommages.\nNous vous demanderons ensuite une description détaillée de l'événement et des mesures correctives prises après celui-ci (ainsi que vos recommandations).\nVous pouvez saisir votre description ou la préparer à l'avance et la coller. Vous pouvez quitter le formulaire et y revenir. Vos réponses sont conservées dans votre navigateur pendant 15 jours ou jusqu'à l'envoi du formulaire."),

        Group(
            "from",
            "From:",
            "De :",
            "Tell us who is reporting the occurrence.",
            "Dites-nous qui déclare l'événement."),
        Field("reporter_first_name", QuestionType.ShortText, isPrivate: true, "from", "First name", "Prénom"),
        Field("reporter_last_name", QuestionType.ShortText, isPrivate: true, "from", "Last name", "Nom de famille"),
        Field("reporter_phone", QuestionType.Phone, isPrivate: true, "from", "Phone number", "Numéro de téléphone"),
        Field("reporter_email", QuestionType.Email, isPrivate: true, "from", "Email", "Courriel"),

        Group(
            "pilot",
            "Pilot:",
            "Pilote :",
            "Tell us who was the pilot in command.",
            "Dites-nous qui était le pilote commandant de bord."),
        Field("pilot_first_name", QuestionType.ShortText, isPrivate: true, "pilot", "First name", "Prénom"),
        Field("pilot_last_name", QuestionType.ShortText, isPrivate: true, "pilot", "Last name", "Nom de famille"),

        Field(
            "pilot_ratings", QuestionType.MultiSelect, isPrivate: true, null,
            "Pilot's ratings:", "Qualifications du pilote :",
            "What are the pilot's most advanced HPAC ratings?",
            "Quelles sont les qualifications ACVL les plus avancées du pilote ?",
            options:
            [
                new SeededOption("student", "Student", "Élève"),
                new SeededOption("p1", "P1", "P1"),
                new SeededOption("p2", "P2", "P2"),
                new SeededOption("p3", "P3", "P3"),
                new SeededOption("p4", "P4", "P4"),
                new SeededOption("h1", "H1", "H1"),
                new SeededOption("h2", "H2", "H2"),
                new SeededOption("h3", "H3", "H3"),
                new SeededOption("h4", "H4", "H4"),
                new SeededOption("paragliding_instructor", "Paragliding Instructor", "Instructeur de parapente"),
                new SeededOption("paragliding_tandem", "Paragliding Tandem", "Parapente biplace"),
                new SeededOption("hang_gliding_instructor", "Hang Gliding Instructor", "Instructeur de deltaplane"),
                new SeededOption("hang_gliding_tandem", "Hang Gliding Tandem", "Deltaplane biplace"),
            ]),

        Field(
            "occurrence_date", QuestionType.Date, isPrivate: true, null,
            "Date:", "Date :",
            "Tell us the date of the occurrence.",
            "Dites-nous la date de l'événement."),

        Field(
            "time_of_day", QuestionType.SingleSelect, isPrivate: false, null,
            "Time of day:", "Moment de la journée :",
            "Tell us the time of the occurrence.",
            "Dites-nous l'heure de l'événement.",
            options:
            [
                new SeededOption("morning", "Morning", "Matin"),
                new SeededOption("mid_day", "Mid-day", "Midi"),
                new SeededOption("afternoon", "Afternoon", "Après-midi"),
                new SeededOption("evening", "Evening", "Soirée"),
                new SeededOption("unknown", "Do not know", "Ne sais pas"),
            ]),

        Field(
            "in_canada", QuestionType.YesNo, isPrivate: false, null,
            "Country:", "Pays :",
            "Did the occurrence happen in Canada?",
            "L'événement s'est-il produit au Canada ?"),

        Field(
            "location", QuestionType.ShortText, isPrivate: true, null,
            "Where:", "Où :",
            "Tell us the location of the occurrence.",
            "Dites-nous le lieu de l'événement."),

        Field(
            "province", QuestionType.SingleSelect, isPrivate: false, null,
            "Province:", "Province :",
            "Tell us the province of the occurrence.",
            "Dites-nous la province de l'événement.",
            options:
            [
                new SeededOption("newfoundland_and_labrador", "Newfoundland and Labrador", "Terre-Neuve-et-Labrador"),
                new SeededOption("prince_edward_island", "Prince Edward Island", "Île-du-Prince-Édouard"),
                new SeededOption("nova_scotia", "Nova Scotia", "Nouvelle-Écosse"),
                new SeededOption("new_brunswick", "New Brunswick", "Nouveau-Brunswick"),
                new SeededOption("quebec", "Quebec", "Québec"),
                new SeededOption("ontario", "Ontario", "Ontario"),
                new SeededOption("manitoba", "Manitoba", "Manitoba"),
                new SeededOption("saskatchewan", "Saskatchewan", "Saskatchewan"),
                new SeededOption("alberta", "Alberta", "Alberta"),
                new SeededOption("british_columbia", "British Columbia", "Colombie-Britannique"),
                new SeededOption("yukon", "Yukon", "Yukon"),
                new SeededOption("northwest_territories", "Northwest Territories", "Territoires du Nord-Ouest"),
                new SeededOption("nunavut", "Nunavut", "Nunavut"),
            ]),

        Group(
            "aircraft",
            "Aircraft:",
            "Aéronef :",
            "Tell us more about the aircraft(s) involved in the occurrence.",
            "Dites-nous-en plus sur le ou les aéronefs impliqués dans l'événement."),
        Field(
            "aircraft_type", QuestionType.MultiSelect, isPrivate: false, "aircraft",
            "Type of aircraft:", "Type d'aéronef :",
            "Tell us the type of aircraft(s) involved in the occurrence.",
            "Dites-nous le type d'aéronef(s) impliqué(s) dans l'événement.",
            options:
            [
                new SeededOption("paraglider", "Paraglider", "Parapente"),
                new SeededOption("hang_glider", "Hang Glider", "Deltaplane"),
                new SeededOption("mini_wing", "Mini Wing", "Mini-voile"),
                new SeededOption("speedflyer", "Speedflyer", "Speedflyer"),
                new SeededOption("paraglider_tandem", "Paraglider Tandem", "Parapente biplace"),
                new SeededOption("hang_glider_tandem", "Hang Glider Tandem", "Deltaplane biplace"),
            ]),
        Field(
            "aircraft_manufacturer", QuestionType.ShortText, isPrivate: true, "aircraft",
            "Manufacturer:", "Fabricant :",
            "Tell us the manufacturer of the pilot's aircraft.",
            "Dites-nous le fabricant de l'aéronef du pilote."),
        Field(
            "aircraft_model", QuestionType.ShortText, isPrivate: true, "aircraft",
            "Model:", "Modèle :",
            "Tell us about the model of the pilot's aircraft.",
            "Dites-nous le modèle de l'aéronef du pilote."),
        Field(
            "aircraft_certification", QuestionType.ShortText, isPrivate: false, "aircraft",
            "Certification:", "Certification :",
            "Tell us about the certification of the pilot's aircraft.",
            "Dites-nous la certification de l'aéronef du pilote."),

        Field(
            "pilot_injury", QuestionType.SingleSelect, isPrivate: false, null,
            "Pilot injury:", "Blessure du pilote :",
            "If any, tell us the type of injury to the pilot.",
            "S'il y a lieu, dites-nous le type de blessure du pilote.",
            options: InjuryOptions()),

        Field(
            "passenger_injury", QuestionType.SingleSelect, isPrivate: false, null,
            "Passenger injury:", "Blessure du passager :",
            "If any, tell us the type of injury to the passenger.",
            "S'il y a lieu, dites-nous le type de blessure du passager.",
            options: InjuryOptions()),

        Field(
            "injury_description", QuestionType.ShortText, isPrivate: false, null,
            "Injury description:", "Description de la blessure :",
            "If any, describe the injury.",
            "S'il y a lieu, décrivez la blessure."),

        Field(
            "damage", QuestionType.ShortText, isPrivate: false, null,
            "Damage:", "Dommages :",
            "If any, describe the damage to the aircraft, environment, or property.",
            "S'il y a lieu, décrivez les dommages à l'aéronef, à l'environnement ou aux biens."),

        Field(
            "description", QuestionType.LongText, isPrivate: false, null,
            "Description:", "Description :",
            "Give a precise description of the occurrence. What happened to the aircraft, what did you see, hear, or do? Include your *role* in the occurrence. Think *preflight*, *weather*, *distractions*, *emotions*. Include your thoughts about the *causes* for the incident or accident.",
            "Donnez une description précise de l'événement. Qu'est-il arrivé à l'aéronef, qu'avez-vous vu, entendu ou fait ? Précisez votre *rôle* dans l'événement. Pensez à la *prévol*, à la *météo*, aux *distractions*, aux *émotions*. Ajoutez vos réflexions sur les *causes* de l'incident ou de l'accident."),

        Field(
            "action_and_prevention", QuestionType.LongText, isPrivate: false, null,
            "Action and prevention:", "Mesures et prévention :",
            "Tell us what actions were taken after the occurrence. Include your thoughts for prevention.",
            "Dites-nous quelles mesures ont été prises après l'événement. Ajoutez vos réflexions sur la prévention."),

        Field(
            "photo_or_video", QuestionType.FileUpload, isPrivate: true, null,
            "Photo or video:", "Photo ou vidéo :",
            "Upload one photo or video of the occurrence. Please contact us directly for multiple files (safety@hpac.ca).",
            "Téléversez une photo ou une vidéo de l'événement. Veuillez nous contacter directement pour plusieurs fichiers (safety@hpac.ca)."),

        // The one system question. It gates publication, so it cannot be
        // deleted, deactivated, or retyped, and it has no default answer.
        new SeededQuestion(
            QuestionKey.ConsentPublish,
            QuestionType.YesNo,
            QuestionRole.ConsentPublish,
            IsPrivate: true,
            IsRequired: true,
            IsSystem: true,
            SectionKey: null,
            "Short form publication",
            "Publication du rapport abrégé",
            "Occurrence reports help our entire community. Do you agree for HPAC to publish a de-identified (no name, location, and other identifiable factors) version of your report on its website?",
            "Les rapports d'événement aident toute notre communauté. Acceptez-vous que l'ACVL publie sur son site Web une version anonymisée (sans nom, lieu ni autre facteur identifiable) de votre rapport ?",
            []),

        // The Typeform recap screen. Its body is a list of Typeform field
        // references, which mean nothing outside Typeform, so the heading is
        // seeded and the body is not. Our own form renders its own review step.
        Statement(
            "answers_summary",
            "Here is a summary of your answers:",
            "Voici un résumé de vos réponses :",
            helpEn: null,
            helpFr: null),
    ];

    private static IReadOnlyList<SeededOption> InjuryOptions() =>
    [
        new SeededOption("none", "No injury", "Aucune blessure"),
        new SeededOption("minor", "Minor injury (no medical aid or on-site aid only)", "Blessure légère (aucun soin médical ou soins sur place seulement)"),
        new SeededOption("serious", "Serious injury (secondary medical aid)", "Blessure grave (soins médicaux subséquents)"),
        new SeededOption("fatality", "Fatality", "Décès"),
        new SeededOption("unknown", "Do not know", "Ne sais pas"),
    ];

    private static SeededQuestion Statement(string key, string labelEn, string labelFr, string? helpEn, string? helpFr) =>
        new(key, QuestionType.Statement, QuestionRole.None, IsPrivate: true,
            IsRequired: false, IsSystem: false, SectionKey: null, labelEn, labelFr, helpEn, helpFr, []);

    private static SeededQuestion Group(string key, string labelEn, string labelFr, string helpEn, string helpFr) =>
        new(key, QuestionType.Group, QuestionRole.None, IsPrivate: true,
            IsRequired: false, IsSystem: false, SectionKey: null, labelEn, labelFr, helpEn, helpFr, []);

    private static SeededQuestion Field(
        string key,
        QuestionType type,
        bool isPrivate,
        string? sectionKey,
        string labelEn,
        string labelFr,
        string? helpEn = null,
        string? helpFr = null,
        QuestionRole role = QuestionRole.None,
        IReadOnlyList<SeededOption>? options = null) =>
        new(key, type, role, isPrivate,
            IsRequired: false, IsSystem: false, sectionKey, labelEn, labelFr, helpEn, helpFr, options ?? []);
}
