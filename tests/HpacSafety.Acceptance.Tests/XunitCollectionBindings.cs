// Reqnroll.xUnit.v3's generator copies a feature's @xunit:collection(Name) tag into
// a Category trait and nothing else, where Reqnroll.xUnit (xUnit v2) turned it into
// [Collection("Name")]. These partial declarations add the attribute the tag asks
// for to the classes Reqnroll generates from the tagged features, so the run-alone
// collections (QuestionBankRunsAlone, MeasuresAllocationRunsAlone) still hold.
// XunitCollectionBindingTests fails when a tagged feature's class has no matching
// attribute, including after a feature is renamed or another one is tagged.
// See ADR-0198.

namespace HpacSafety.Acceptance.Tests.Spec.Features.Dependent_Choices
{
	[Collection(QuestionBankRunsAlone.Name)]
	public partial class DependentChoicesFeature;
}

namespace HpacSafety.Acceptance.Tests.Spec.Features.Question_Translation
{
	[Collection(QuestionBankRunsAlone.Name)]
	public partial class QuestionTranslationFeature;
}

namespace HpacSafety.Acceptance.Tests.Spec.Features.Question_Authoring
{
	[Collection(QuestionBankRunsAlone.Name)]
	public partial class QuestionAuthoringFeature;
}

namespace HpacSafety.Acceptance.Tests.Spec.Features.Choices_And_Type_Ahead
{
	[Collection(QuestionBankRunsAlone.Name)]
	public partial class ChoicesAndType_AheadValuesFeature;
}

namespace HpacSafety.Acceptance.Tests.Spec.Features.Report_Form
{
	[Collection(QuestionBankRunsAlone.Name)]
	public partial class ReportFormFeature;
}

namespace HpacSafety.Acceptance.Tests.Spec.Features.Media
{
	[Collection(MeasuresAllocationRunsAlone.Name)]
	public partial class AttachmentsFeature;
}
