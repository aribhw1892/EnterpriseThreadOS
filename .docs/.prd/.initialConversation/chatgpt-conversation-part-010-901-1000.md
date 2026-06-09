## 901. assistant

Confirmed.

Governed chat will support:

Free-form user question
→ Intent detection
→ Approved QueryIntentVersion
→ Allowed RetrievalStrategyVersion
→ Governed Context Assembly
→ ContextPackage
→ Answer + AI Trace

If no approved intent matches:

Safe general search
or
Ask for refinement
or
Create Draft QueryIntent proposal

Next question:

Should unmatched free-form questions be allowed to use raw LLM reasoning over retrieved context, or should they be restricted to safe general retrieval only?

My recommended answer:
Use safe general retrieval only, not unrestricted reasoning.

Reason: unmatched intent means the system does not yet know the business purpose, allowed artifact types, retrieval budget, or evidence rules. It should stay conservative until the intent is approved or refined.

---

## 902. user

Ues

---

## 903. assistant

Confirmed.

Unmatched free-form questions will use safe general retrieval only.

No approved intent means:

No broad graph traversal
No high-risk evidence use
No unrestricted raw LLM reasoning
No recommendation/task creation

Next question:

Should unmatched questions be logged as candidate query intents for future review?

My recommended answer:
Yes. Store them as QueryIntentCandidate records with the user question, detected topic, attempted retrieval path, missing intent reason, and suggested draft intent.

Reason: repeated unmatched questions reveal what query intents the platform should add next.

---

## 904. user

Yes

---

## 905. assistant

Confirmed.

QueryIntentCandidate Framework

When no approved intent matches:

User Question
      ↓
Intent Detection
      ↓
No Approved Match
      ↓
QueryIntentCandidate

Structure:

QueryIntentCandidate
├── CandidateId
├── OriginalQuestion
├── SuggestedIntentName
├── SuggestedIntentCategory
├── DetectedArtifacts
├── SuggestedRetrievalStrategy
├── MissingIntentReason
├── FrequencyCount
├── LastSeen
├── Confidence
├── TenantId
└── Status
Learning Loop
Unmatched Question
      ↓
QueryIntentCandidate
      ↓
Repeated Usage
      ↓
LearningEvidence
      ↓
Review Task
      ↓
Draft QueryIntentVersion
      ↓
Approval
      ↓
Published QueryIntentVersion
Benefits
Platform learns user needs
Identifies missing intents
Improves onboarding
Guides ontology evolution
Guides semantic layer evolution
Improves retrieval coverage
Milestone 3 Status

We have now defined:

QueryIntentVersion
RetrievalStrategyVersion

ContextPackage
ContextAccessDecision
RetrievalRun

Governed Context Assembly

AI Trace Foundation

Governed Chat

QueryIntentCandidate

Artifact Explorer
Graph Explorer
Document Explorer
AI Trace Explorer

360° Context View

Governance Flow Foundation

This completes the first AI Consumption Layer of the platform.

Next Question (Transition toward Milestone 4)

Should Milestone 4 focus on Recommendations, Review Tasks, Decisions, and Outcome Tracking before Agents and Workflows?

My recommended answer:

Yes.

Order:

Milestone 4
├── RecommendationArtifact
├── ReviewTaskArtifact
├── DecisionArtifact
├── OutcomeCheckRun
├── LearningSignalArtifact
├── Governance Flow Expansion
└── Decision Intelligence

Milestone 5
├── AgentVersion
├── WorkflowVersion
├── Agent Kernel
├── Workflow Runtime
├── Tool Gateway
├── Multi-Agent Collaboration
└── Advanced Automation

Reason:

Agents and workflows ultimately exist to create value through:

Recommendations
Reviews
Decisions
Outcomes
Learning

Building the governance and decision intelligence layer first gives the agents a governed destination when they arrive.

Do you agree with this milestone sequencing?

---

## 906. user

Yes

---

## 907. assistant

Confirmed.

This sequencing is the strongest path and aligns with the architecture we've built.

Milestone 4 — Decision Intelligence & Governance

Focus:

RecommendationArtifact
ReviewTaskArtifact
DecisionArtifact
OutcomeCheckRun
LearningEvidence
LearningSignalArtifact
Governance Flow Expansion
Decision Intelligence

Primary goal:

Knowledge
    ↓
Recommendation
    ↓
Review
    ↓
Decision
    ↓
Outcome
    ↓
Learning

This milestone transforms the platform from:

AI Search Platform

into:

AI Governance & Decision Platform
Milestone 5 — Agents & Workflows

Focus:

AgentVersion
WorkflowVersion

Agent Capability Profiles
Agent Trust Profiles

Agent Kernel
Workflow Runtime

Tool Gateway

Agent Interaction Runs

Multi-Agent Collaboration

Safe Mode
Fallback Logic
Execution Governance

Primary goal:

Governed Knowledge
      ↓
Governed Decisions
      ↓
Governed Automation

This prevents us from building agents before we have a governed destination for their outputs.

Current High-Level Roadmap
Milestone 1 — Platform Foundation
Identity
Tenancy
Artifacts
Graph Foundation
Audit
Classification
Infrastructure
Admin UI
Milestone 2 — Knowledge Foundation
Ontology
Semantic Layer
Model Packages
Import Framework
Identity Resolution
Graph Snapshots
Data Quality
Documents
Learning Evidence
Milestone 3 — AI Consumption Layer
Query Intents
Retrieval Strategies
Context Assembly
Governed Chat
AI Trace
Explorers
360° Context
Governance Flow Foundation
Milestone 4 — Decision Intelligence
Recommendations
Review Tasks
Decisions
Outcome Tracking
Learning Signals
Governance Expansion
Milestone 5 — Agentic Platform
Agents
Workflows
Agent Kernel
Tool Gateway
Multi-Agent Runtime
Execution Governance
Future Milestones
Enterprise Actions
External Collaboration
Supplier Portal
Customer Portal
Advanced Learning Models
Marketplace
Cross-Tenant Benchmarking
Industry Knowledge Packs
Next Question

Within Milestone 4, should we build:

RecommendationArtifact

first, or:

ReviewTaskArtifact

first?

My recommended answer:

RecommendationArtifact first.

Reason:

Knowledge
    ↓
Recommendation
    ↓
Review Task
    ↓
Decision

Review tasks exist because recommendations exist. The recommendation becomes the root object that starts the governance chain.

---

## 908. user

Yes

---

## 909. assistant

Confirmed.

Milestone 4 starts with:

RecommendationArtifact

Next question:

Should RecommendationArtifact be created only by AI, or also manually by users?

My recommended answer:
Both.

Allow recommendations to be created by:

AI chat
Query intent result
Graph diff rule
Data-quality issue
Document analysis
Manual user entry
Future agent/workflow

Reason: recommendations are the governance starting point. Some come from AI, but users also need to capture expert judgment manually.

---

## 910. user

Yes

---

## 911. assistant

Confirmed.

RecommendationArtifact can be created from:

AI chat
Query intent result
Graph diff rule
Data-quality issue
Document analysis
Manual user entry
Future agent/workflow

Next question:

Should every RecommendationArtifact require evidence references before it can move from Draft to Reviewed?

My recommended answer:
Yes.

A recommendation should include at least one of:

Graph evidence
Document evidence
DataQualityIssueArtifact
GraphDiff result
ImportBatch
AI Trace
Manual evidence note

Reason: recommendations without evidence become opinions. The platform should enforce evidence-backed governance.

---

## 912. user

Yes

---

## 913. assistant

Confirmed.

A RecommendationArtifact must be evidence-backed before it can move from Draft to Reviewed.

Next question:

Should manually created recommendations require the same evidence standard as AI-generated recommendations?

My recommended answer:
Yes, but allow a manual evidence note as valid evidence in MVP.

Reason: users may know business context that is not yet captured in the graph/documents, but the recommendation should still record why it exists.

---

## 914. user

Yes

---

## 915. assistant

Confirmed.

Manual recommendations follow the same evidence standard, with manual evidence note allowed in MVP.

Next question:

Should RecommendationArtifact have a structured recommendation type taxonomy?

My recommended answer:
Yes.

Examples:

Data Quality Recommendation
BOM Sync Recommendation
Rework Risk Recommendation
Identity Resolution Recommendation
Document Link Recommendation
Security Recommendation
Policy Recommendation
Artifact Upgrade Recommendation

Reason: recommendation type drives review task template, priority rules, confidence scoring, governance flow, dashboards, reports, and learning signals.

---

## 916. user

Yes, all will inherit from base artifact right??

---

## 917. assistant

Yes. Correct.

All recommendation types are still:

RecommendationArtifact : BaseArtifact

The type taxonomy is just metadata/configuration on the artifact, for example:

RecommendationArtifact
├── BaseArtifact fields
├── RecommendationType
├── Severity
├── Priority
├── DataConfidence
├── ExecutionConfidence
├── FinalConfidence
├── EvidenceReferences
├── SuggestedActions
└── Status

So we do not create separate base classes like:

BOMSyncRecommendationArtifact
ReworkRiskRecommendationArtifact

unless needed later. MVP should keep one generic RecommendationArtifact with configurable RecommendationType.

---

## 918. user

Okk

---

## 919. assistant

Exactly.

This is actually one of the key architectural principles we've converged on:

Prefer Configuration + Taxonomy over Class Explosion

Instead of:

RecommendationArtifact
├── BOMSyncRecommendationArtifact
├── ReworkRiskRecommendationArtifact
├── IdentityResolutionRecommendationArtifact
├── SecurityRecommendationArtifact
├── PolicyRecommendationArtifact
├── DocumentRecommendationArtifact
└── ...

We use:

RecommendationArtifact : BaseArtifact
    └── RecommendationType

where:

RecommendationType
├── BOM_SYNC
├── REWORK_RISK
├── IDENTITY_RESOLUTION
├── DOCUMENT_LINK
├── SECURITY
├── POLICY
├── ARTIFACT_UPGRADE
└── ...

This follows the same pattern we already established for:

ReviewTaskArtifact
├── TaskType

DecisionArtifact
├── DecisionType

SecurityEventArtifact
├── EventType

DataQualityIssueArtifact
├── IssueType

QueryIntentVersion
├── IntentCategory

RetrievalStrategyVersion
├── StrategyType
Why This Is Important

Because every artifact already inherits:

BaseArtifact
├── Versioning
├── Status
├── Classification
├── Audit
├── Explainability
├── Relationships
├── Dependencies
├── Confidence
├── Governance
└── Security

If we create separate classes for every subtype, we'll eventually end up with hundreds of artifact classes.

Instead:

Artifact Class
    +
Artifact Type
    +
Artifact Template
    +
Artifact Metadata

gives us the same flexibility while keeping the domain model manageable.

Refined Principle

For MVP:

Few Artifact Classes
Many Artifact Types

Future (when we add custom artifact definitions):

Few Artifact Classes
Many Artifact Types
Tenant-defined Types

This is much more aligned with the metadata-driven platform you're building.

Next question

Should RecommendationType be a simple enum in MVP, or should it already be a governed/versioned artifact (RecommendationTypeDefinition)?

My recommendation:

MVP
→ Enum / seeded configuration

Future
→ RecommendationTypeDefinition : BaseArtifact

because we already decided that custom artifact definitions are a future capability.

---

## 920. user

Okk

---

## 921. assistant

Confirmed.

For MVP:

RecommendationType
→ Enum / seeded configuration

Examples:

DATA_QUALITY
BOM_SYNC
REWORK_RISK
IDENTITY_RESOLUTION
DOCUMENT_LINK
SECURITY
POLICY
ARTIFACT_UPGRADE
LIFECYCLE_CONFLICT
IMPORT_VALIDATION

Future:

RecommendationTypeDefinition : BaseArtifact

This is consistent with another principle we've established:

MVP
→ Seeded taxonomies/configuration

Future
→ Governed versioned definitions

This approach should also apply to:

RecommendationType
ReviewTaskType
DecisionType
IssueType
SecurityEventType
IntentCategory
StrategyType
OutcomeCategory

so we don't prematurely build a dynamic taxonomy framework before the core platform is working.

Next Question

Should RecommendationArtifact support multiple suggested actions or only a single recommended action?

My recommended answer:

Multiple suggested actions.

Example:

Recommendation:
CAD BOM changed

Suggested Actions:
1. Review EBOM synchronization
2. Review affected documents
3. Review manufacturing impact
4. Create change request

Reason:

Recommendations often identify a problem, but there can be several valid remediation paths. The decision-maker should choose among them rather than the recommendation forcing a single action.

---

## 922. user

Yes

---

## 923. assistant

Confirmed.

RecommendationArtifact supports multiple suggested actions.

Next question:

Should each suggested action have its own risk, confidence, and required review path?

My recommended answer:
Yes.

Example:

Suggested Action 1: Review EBOM synchronization
Risk: Medium
Confidence: High
Review Path: Engineering Review

Suggested Action 2: Create future PLM change request
Risk: High
Confidence: Medium
Review Path: Engineering + Governance Approval

Reason: one recommendation may contain safe review actions and higher-risk future actions. Each action needs its own governance metadata.

---

## 924. user

Yes

---

## 925. assistant

Confirmed.

This is the right level of governance granularity.

RecommendationArtifact Structure
RecommendationArtifact
├── RecommendationType
├── Severity
├── Priority
├── DataConfidence
├── ExecutionConfidence
├── FinalConfidence
├── EvidenceReferences
├── SuggestedActions[]
└── Status
SuggestedAction

Each action becomes its own governed object within the recommendation:

SuggestedAction
├── ActionId
├── Title
├── Description
├── RiskScore
├── RiskCategory
├── DataConfidence
├── ExecutionConfidence
├── FinalConfidence
├── RequiredReviewPath
├── RequiredApprovals
├── EstimatedImpact
├── EstimatedEffort
├── Dependencies
├── Status
└── Metadata

Example:

Recommendation:
CAD BOM changed

Suggested Action 1
├── Review EBOM Synchronization
├── Risk: Medium
├── Confidence: 0.92
└── Review Path: Engineering Review

Suggested Action 2
├── Review Manufacturing Impact
├── Risk: High
├── Confidence: 0.81
└── Review Path: Manufacturing Review

Suggested Action 3
├── Create Change Request
├── Risk: High
├── Confidence: 0.75
└── Review Path: Engineering + Governance
Future Benefit

This directly supports the future architecture:

Recommendation
       ↓
Suggested Action
       ↓
Review Task
       ↓
Decision
       ↓
Outcome

without forcing every recommendation into a single workflow.

Next Question

Should a ReviewTaskArtifact be created from the Recommendation itself, or from a specific SuggestedAction?

My recommended answer:

From a specific SuggestedAction.

Example:

Recommendation
├── Action A
├── Action B
└── Action C

User chooses Action B

→ ReviewTask created for Action B

Reason:

The recommendation is the analysis.

The suggested action is the proposed course of action.

The review task should govern the chosen action, not the entire recommendation. This keeps decisions precise and traceable.

---

## 926. user

Yes

---

## 927. assistant

Confirmed.

This is a very important distinction:

Recommendation
≠
Review Task

The recommendation is the analysis and set of possible actions.

The review task governs a selected action.

Governance Flow
RecommendationArtifact
├── SuggestedAction A
├── SuggestedAction B
└── SuggestedAction C

User / AI / Policy chooses:

SuggestedAction B

Then:

SuggestedAction B
        ↓
ReviewTaskArtifact
        ↓
DecisionArtifact
        ↓
OutcomeCheckRun
        ↓
LearningEvidence
        ↓
LearningSignalArtifact
Relationships
RecommendationArtifact
        ──HAS_ACTION──>
SuggestedAction

ReviewTaskArtifact
        ──CREATED_FROM──>
SuggestedAction

DecisionArtifact
        ──DECIDES──>
ReviewTaskArtifact

This gives much cleaner traceability:

Recommendation
    ↓
Chosen Action
    ↓
Review
    ↓
Decision

instead of:

Recommendation
    ↓
Review entire recommendation

which becomes ambiguous when multiple actions exist.

Next Question

Should SuggestedAction be an embedded child object inside RecommendationArtifact, or a first-class artifact itself?

My recommended answer:

MVP
SuggestedAction
→ Embedded governed child object

inside:

RecommendationArtifact
Future

If enterprise actions become sophisticated:

SuggestedActionArtifact : BaseArtifact

Reason:

In MVP, making every suggested action a full artifact would create unnecessary complexity and artifact explosion.

A governed child object is sufficient until we reach the future ActionPlan / Enterprise Action milestone.

---

## 928. user

Yes

---

## 929. assistant

Confirmed.

For MVP:

SuggestedAction
→ embedded governed child object inside RecommendationArtifact

Future:

SuggestedActionArtifact : BaseArtifact

only if action planning becomes complex enough.

Next question:

Should each SuggestedAction have a lifecycle/status inside the RecommendationArtifact?

My recommended answer:
Yes.

Use lightweight statuses:

Proposed
SelectedForReview
Rejected
ConvertedToReviewTask
Deferred
Superseded

Reason: users may choose one action, reject another, defer another, or later convert one into a review task. That needs traceability without making SuggestedAction a full artifact yet.

---

## 930. user

Yes

---

## 931. assistant

Confirmed.

SuggestedAction will have lightweight internal status:

Proposed
SelectedForReview
Rejected
ConvertedToReviewTask
Deferred
Superseded

Next question:

Should changing a SuggestedAction status create an audit record?

My recommended answer:
Yes.

Reason: selecting, rejecting, deferring, or converting a suggested action changes the governance path and may influence decisions, task creation, and learning memory.

---

## 932. user

Yes

---

## 933. assistant

Confirmed.

SuggestedAction status changes will create audit records.

Next question:

Should rejected or deferred SuggestedActions create learning evidence?

My recommended answer:
Yes.

But only as lower-level LearningEvidence, not immediate LearningSignalArtifact.

Reason: rejected/deferred actions reveal user judgment and business preference, but only repeated patterns should become formal learning signals.

---

## 934. user

Yes

---

## 935. assistant

Confirmed.

Rejected or deferred SuggestedActions will create:

LearningEvidence

immediately, and only contribute to:

LearningSignalArtifact

when meaningful patterns emerge.

Example:

Recommendation Type:
BOM_SYNC

Suggested Action:
Create Change Request

Rejected 1 time
→ LearningEvidence

Rejected 50 times across similar situations
→ LearningSignalArtifact

Potential Finding:
Users prefer BOM review before change request creation

This fits our established learning hierarchy:

Decision
Action Selection
Action Rejection
Action Deferral
        ↓
LearningEvidence
        ↓
Pattern Detection
        ↓
LearningSignalArtifact
        ↓
LearningModel
        ↓
LearningPolicy
Recommendation Module Status

We have now defined:

RecommendationArtifact
├── RecommendationType
├── EvidenceReferences
├── Confidence Scores
├── Severity
├── Priority
└── SuggestedActions[]

SuggestedAction
├── Risk
├── Confidence
├── Review Path
├── Approvals
├── Dependencies
├── Status
└── Audit

Governance flow:

Recommendation
      ↓
Suggested Action
      ↓
Review Task
      ↓
Decision
      ↓
Outcome
      ↓
Learning

This completes the Recommendation foundation and positions us to move into the next major component of Milestone 4:

ReviewTaskArtifact
Next Question

Should ReviewTaskArtifact always be linked to a Recommendation/SuggestedAction, or should users also be able to create standalone Review Tasks?

My recommended answer:

Both.

Sources:

Recommendation SuggestedAction
DataQualityIssueArtifact
SecurityEventArtifact
Identity Resolution Review
Import Validation Review
Manual User Review Task

Reason:

Many reviews originate from recommendations, but not all.

Examples:

Supplier Audit Review
Security Investigation
Manual Engineering Review
Import Approval Review

may need ReviewTaskArtifacts even when no recommendation exists.

---

## 936. user

Yes

---

## 937. assistant

Confirmed.

This is the correct balance.

ReviewTaskArtifact Creation Sources
Recommendation-Driven
RecommendationArtifact
        ↓
SuggestedAction
        ↓
ReviewTaskArtifact

Examples:

BOM Sync Review
Rework Risk Review
Document Link Review
Policy Review
Direct Governance Sources
DataQualityIssueArtifact
        ↓
ReviewTaskArtifact

SecurityEventArtifact
        ↓
ReviewTaskArtifact

IdentityCandidate
        ↓
ReviewTaskArtifact

ImportBatch
        ↓
ReviewTaskArtifact

Examples:

Import Approval Review
Identity Match Review
Security Investigation Review
Data Quality Review
Manual Sources
User
        ↓
ReviewTaskArtifact

Examples:

Engineering Review
Supplier Review
Architecture Review
Governance Review
Management Review
ReviewTaskArtifact Structure
ReviewTaskArtifact : BaseArtifact

├── ReviewTaskType
├── SourceType
├── SourceReference
├── AssignedToUser
├── AssignedToRole
├── Priority
├── Severity
├── DataConfidence
├── ExecutionConfidence
├── FinalConfidence
├── Status
├── EvidenceReferences
├── RequiredDecisionType
├── OutcomeOptions
├── ReviewTemplateVersion
├── DueDate (optional)
├── Escalation placeholders
└── AuditReferences
Source Types
RECOMMENDATION_ACTION
DATA_QUALITY_ISSUE
SECURITY_EVENT
IDENTITY_CANDIDATE
IMPORT_BATCH
MANUAL
SYSTEM

This allows us to trace:

ReviewTask
        ↓
Why does this task exist?

without forcing every review task to originate from a recommendation.

Next Question

Should ReviewTaskArtifact support multiple assignees, or only one primary owner in MVP?

My recommended answer:

One primary owner + optional participant list.

AssignedToUser (primary)

Participants[]

Reason:

Most reviews need clear ownership.

Allowing multiple owners causes ambiguity:

Who is responsible?

Participants provide collaboration without diluting accountability.

---

## 938. user

Yes

---

## 939. assistant

Confirmed.

Review tasks will use:

Primary owner
+
Optional participants

Next question:

Should participants have roles inside a ReviewTaskArtifact?

My recommended answer:
Yes.

Example:

PrimaryOwner
Reviewer
Approver
Observer
Contributor
EscalationContact

Reason: participants may need different responsibilities and permissions inside the same review task.

---

## 940. user

Yes

---

## 941. assistant

Confirmed.

ReviewTask participants will have task-level roles:

PrimaryOwner
Reviewer
Approver
Observer
Contributor
EscalationContact

Next question:

Should task-level roles affect what each participant can do inside the ReviewTaskArtifact?

My recommended answer:
Yes.

Example:

PrimaryOwner
→ manage task, submit decision

Reviewer
→ review evidence, comment

Approver
→ approve/reject decision

Observer
→ view only

Contributor
→ add evidence/comment

EscalationContact
→ notified on escalation only

Reason: task participation should be governed by responsibility, not just access to the task.

---

## 942. user

Yes

---

## 943. assistant

Confirmed.

Task-level roles will be permission-aware and responsibility-aware.

ReviewTask Participant Permissions
PrimaryOwner
View task
Manage task
Assign participants
Update status
Submit recommendation for decision
Add evidence
Add comments
Request review
Reviewer
View task
Review evidence
Add comments
Add evidence
Provide review outcome
Approver
View task
Review evidence
Approve
Reject
Request changes
Contributor
View task
Add evidence
Add comments
Suggest updates
Observer
View only
EscalationContact
View task
Receive escalation notifications
Participate after escalation
Important Governance Rule

Task-level permissions do not bypass platform security.

Final permissions become:

User Permission
+
Role Permission
+
Task Role Permission
+
Classification Access
+
ABAC Rules
+
Tenant Isolation

Example:

User is Reviewer

Task contains Restricted evidence

User lacks classification permission

Result:
Cannot view restricted evidence
Even though Reviewer role exists

This preserves the security model we've already established.

ReviewTask Module Status

We have now defined:

ReviewTaskArtifact

ReviewTaskType
ReviewTaskTemplateVersion

Primary Owner
Participants

Participant Roles

Status Model

Confidence Model

Evidence Model

Decision Integration

Learning Integration

Task Chaining

Blocking Relationships

Governance Flow Integration

The next major piece of Milestone 4 is:

DecisionArtifact
Next Question

Should a ReviewTaskArtifact always result in a DecisionArtifact, or can some tasks be closed without a decision?

My recommended answer:

Every completed ReviewTaskArtifact should result in a DecisionArtifact.

Even if the outcome is:

No Action Needed
Rejected
Duplicate
Deferred
Known Exception

that is still a decision.

Reason:

Without a DecisionArtifact:

Review completed
↓
Why?

is lost.

The decision layer is the organization's memory of what was decided and why.

Do you agree that all completed review tasks should produce a DecisionArtifact, even when the outcome is "no action" or "rejected"?

---

## 944. user

Yes

---

## 945. assistant

Confirmed.

This is a foundational governance rule.

Review → Decision Principle
Completed ReviewTaskArtifact
        ↓
DecisionArtifact

Always.

No exceptions.

Valid Decision Outcomes

Examples:

Approved
Rejected
No Action Needed
Known Exception
Deferred
Duplicate
Escalated
Superseded
Requires More Information
Requires Additional Review

Even:

No Action Needed

is still:

A Decision

and must be recorded.

Why This Matters

Without a decision:

Review Task Closed
        ↓
Unknown Outcome

With a decision:

Review Task Closed
        ↓
DecisionArtifact
        ↓
Reason
        ↓
Evidence
        ↓
Outcome
        ↓
Learning

This preserves:

Auditability
Explainability
Governance
Learning
Organizational Memory
DecisionArtifact Structure
DecisionArtifact : BaseArtifact

├── DecisionType
├── DecisionOutcome
├── DecisionCategory
├── DecisionReason
├── EvidenceReferences
├── ConfidenceScores
├── DecidedBy
├── ApprovedBy
├── DecisionDate
├── RelatedReviewTask
├── RelatedRecommendation
├── RelatedSuggestedAction
├── RelatedDataQualityIssue
├── RelatedSecurityEvent
├── RelatedImportBatch
├── RelatedIdentityResolution
├── OutcomeTrackingRequired
├── FollowUpActions
├── Status
└── AuditReferences
Relationship Model
RecommendationArtifact
        ↓
SuggestedAction
        ↓
ReviewTaskArtifact
        ↓
DecisionArtifact
        ↓
OutcomeCheckRun
        ↓
LearningEvidence
        ↓
LearningSignalArtifact

This becomes the primary governance chain of the platform.

Next Question

Should DecisionArtifact support single-person decisions only, or also multi-approver decisions in MVP?

My recommended answer:

Support both in MVP.

Simple case:

Reviewer
→ Decision

Multi-approver case:

Engineering Approval
+
Quality Approval
+
Governance Approval
        ↓
Decision

Store:

DecisionParticipants
DecisionVotes
DecisionApprovals
ApprovalStatus

Reason:

Many manufacturing and governance workflows require more than one approver, and adding this later would impact the core decision model.

---

## 946. user

Yes

---

## 947. assistant

Confirmed.

DecisionArtifact will support:

Single-person decision
+
Multi-approver decision

MVP decision model should include:

DecisionParticipants
DecisionVotes
DecisionApprovals
ApprovalStatus
RequiredApproverRoles
ApprovalRule
FinalDecisionOutcome

Next question:

Should multi-approver decisions require unanimous approval, majority approval, or configurable approval rules?

My recommended answer:
Use configurable approval rules, with simple MVP options:

Single approver
All required approvers
Any one approver
Majority approval
Role-based approval

Reason: engineering, quality, security, and governance reviews may need different approval logic.

---

## 948. user

Yes

---

## 949. assistant

Confirmed.

Multi-approver DecisionArtifact will support configurable MVP approval rules:

Single approver
All required approvers
Any one approver
Majority approval
Role-based approval

Next question:

Should approval rules be defined inside each ReviewTaskTemplateVersion, or inside DecisionArtifact itself?

My recommended answer:
Define approval rules in ReviewTaskTemplateVersion, then copy the applied rule snapshot into the DecisionArtifact.

Reason: the template defines the expected governance process, while the decision must preserve the exact rule used at the time even if the template changes later.

---

## 950. user

Yes

---

## 951. assistant

Confirmed.

Approval rule handling:

ReviewTaskTemplateVersion
→ defines approval rule

DecisionArtifact
→ stores applied approval rule snapshot

This preserves historical explainability even if the template changes later.

Next question:

Should decision votes/comments from approvers be stored individually?

My recommended answer:
Yes. Store each approver’s vote, comment, timestamp, confidence, and any evidence they referenced.

Reason: the final decision is important, but the approval reasoning behind it is equally important for audit, learning, and future reviews.

---

## 952. user

Yes

---

## 953. assistant

Confirmed.

Decision approver records will store:

Approver
Vote
Comment/reason
Timestamp
Confidence
Referenced evidence
Approval rule snapshot
Audit reference

Next question:

Should conflicting approver votes create a blocked decision state?

My recommended answer:
Yes.

Example:

Engineering: Approve
Quality: Reject
Governance: Approve

Decision status:
Blocked / Conflict

Reason: conflicting decisions should not be silently resolved unless the approval rule explicitly allows majority approval or escalation.

---

## 954. user

Yes

---

## 955. assistant

Confirmed.

Conflicting approver votes will create a blocked/conflict decision state unless the approval rule explicitly allows majority approval or escalation.

Next question:

Should blocked/conflict decisions automatically create an escalation review task?

My recommended answer:
Yes, but only when the ReviewTaskTemplateVersion defines an escalation path.

Reason: some conflicts should be resolved by an escalation owner, but others may simply require more evidence or re-review. The template should define the correct escalation behavior.

---

## 956. user

Yes

---

## 957. assistant

Confirmed.

Escalation review tasks will be created from blocked/conflict decisions only when the active ReviewTaskTemplateVersion defines an escalation path.

Next question:

Should escalated decisions preserve the original conflicting votes, or create a fresh decision record?

My recommended answer:
Preserve the original conflicting decision record and create an escalation child decision if needed.

DecisionArtifact
  └── Status: Blocked / Conflict
      ↓
EscalationReviewTask
      ↓
EscalationDecisionArtifact
          └── RESOLVES → Original DecisionArtifact

Reason: this keeps the original disagreement visible while allowing escalation to resolve it cleanly.

---

## 958. user

Yes

---

## 959. assistant

Confirmed.

Escalation preserves the original conflict and creates a child resolution path:

Original DecisionArtifact
├── Status: Blocked / Conflict
├── Preserved votes/comments/evidence
└── EscalationReviewTask
      ↓
   EscalationDecisionArtifact
      └── RESOLVES → Original DecisionArtifact

Next question:

Should escalation decisions be allowed to override the original decision outcome?

My recommended answer:
Yes, but only through an explicit escalation authority rule defined in the ReviewTaskTemplateVersion.

Reason: escalation should not be an informal bypass. The system must know who has authority to resolve conflicts and under what conditions.

---

## 960. user

Ues

---

## 961. assistant

Confirmed.

Escalation decisions can override original outcomes only when the ReviewTaskTemplateVersion defines explicit escalation authority.

Next question:

Should every DecisionArtifact require outcome tracking, or only selected decision types?

My recommended answer:
Only selected decision types require outcome tracking.

Required for:

Rework risk decisions
BOM sync decisions
Data-quality override decisions
Security/policy decisions
Artifact publish override decisions
High-severity recommendations

Optional for:

Low-risk manual reviews
Informational decisions
No-action decisions

Reason: outcome tracking is valuable, but forcing it for every small decision may create unnecessary workload.

---

## 962. user

Yes

---

## 963. assistant

Confirmed.

Outcome tracking will be selective, not mandatory for every decision.

Decision → Outcome Tracking Rules
Outcome Tracking Required
REWORK_RISK
BOM_SYNC
DATA_QUALITY_OVERRIDE
SECURITY_DECISION
POLICY_DECISION
PUBLISH_OVERRIDE
HIGH_SEVERITY_RECOMMENDATION
IDENTITY_RESOLUTION_OVERRIDE
IMPORT_OVERRIDE

These create:

DecisionArtifact
        ↓
OutcomeCheckRun
Outcome Tracking Optional
NO_ACTION
INFORMATIONAL
LOW_RISK_REVIEW
MANUAL_NOTE
DUPLICATE
KNOWN_EXCEPTION

Outcome tracking may still be created manually.

OutcomeCheckRun Structure
OutcomeCheckRun
├── OutcomeCheckId
├── DecisionArtifact
├── CheckType
├── ExpectedOutcome
├── ActualOutcome
├── OutcomeStatus
├── OutcomeConfidence
├── MeasuredAt
├── EvidenceReferences
├── Variance
├── PerformedBy
└── AuditReferences
Example
Decision:
Approve BOM Synchronization

Expected Outcome:
CAD BOM and EBOM aligned

30 Days Later:

OutcomeCheckRun
→ Alignment = 98%

Status:
Successful

or

Expected Outcome:
Reduce duplicate suppliers

Actual Outcome:
No measurable improvement

Status:
Failed
Learning Loop
DecisionArtifact
        ↓
OutcomeCheckRun
        ↓
LearningEvidence
        ↓
LearningSignalArtifact
        ↓
LearningModel

This closes the governance loop we have been building since the beginning:

Knowledge
      ↓
Recommendation
      ↓
Review
      ↓
Decision
      ↓
Outcome
      ↓
Learning
Next Question

Should OutcomeCheckRun schedules be fixed by decision type, or configurable per decision?

My recommended answer:

Use template defaults + per-decision override.

Example:

BOM_SYNC
→ Default: 30 days

REWORK_RISK
→ Default: 90 days

SECURITY
→ Default: 7 days

But decision makers may override:

Outcome Check
→ 14 days
→ 60 days
→ 180 days

with audit.

Reason:

Different decisions need different observation windows, and some situations require custom monitoring periods.

---

## 964. user

Yes

---

## 965. assistant

Confirmed.

Outcome tracking schedule model:

ReviewTaskTemplateVersion
DecisionType
        ↓
Default Outcome Schedule

DecisionArtifact
        ↓
Optional Override
        ↓
Audit Record

Examples:

BOM_SYNC
→ 30 days

REWORK_RISK
→ 90 days

SECURITY
→ 7 days

DATA_QUALITY_OVERRIDE
→ 30 days

Override example:

Default = 30 days
Override = 60 days
Reason = Major Release Window
Audit = Recorded

Next question:

Should OutcomeCheckRun be executed manually, automatically, or both?

My recommended answer:
Both.

Automatic:

Graph diff checks
Data quality checks
Identity consistency checks
Document linkage checks
Policy compliance checks

Manual:

Business outcome review
Engineering assessment
Management review
Supplier review
Customer impact review

Reason: some outcomes can be measured automatically from graph/document data, while others require human judgment.

---

## 966. user

Yes

---

## 967. assistant

Confirmed.

Outcome checks can be:

Automatic
+
Manual

Next question:

Should automatic OutcomeCheckRuns create AI-suggested outcomes that require human confirmation?

My recommended answer:
Yes.

Flow:

Automatic outcome check
→ AI suggested outcome
→ Evidence summary
→ Confidence score
→ Human confirmation
→ LearningEvidence

Reason: automatic checks can detect signals, but business outcome confirmation should stay human-governed in MVP.

---

## 968. user

Yes

---

## 969. assistant

Confirmed.

Automatic OutcomeCheckRun will produce:

AI-suggested outcome
Evidence summary
Confidence score
Human confirmation request

Only after human confirmation does it become trusted learning evidence.

Next question:

Should unconfirmed AI-suggested outcomes be allowed to influence learning memory?

My recommended answer:
No. Store them as pending outcome suggestions only.

Reason: learning memory should be based on confirmed decisions/outcomes, not unverified AI interpretations.

---

## 970. user

Correct

---

## 971. assistant

Confirmed.

Unconfirmed AI-suggested outcomes will not influence learning memory.

They remain:

PendingOutcomeSuggestion

until a human confirms them.

Next question:

Should pending outcome suggestions expire if not reviewed within a defined period?

My recommended answer:
Yes, but as a placeholder in MVP.

MVP:

Pending status
Review reminder field
Expiration metadata
No automated expiry workflow yet

Future:

Auto-expire
Notify owner
Escalate overdue outcome suggestions
Create governance review task

---

## 972. user

Yes

---

## 973. assistant

Confirmed.

Pending outcome suggestions in MVP will include:

Pending status
Review reminder field
Expiration metadata
No automated expiry workflow yet

Future:

Auto-expire
Notify owner
Escalate overdue suggestions
Create governance review task

Next question:

Should confirmed outcome results automatically create LearningEvidence?

My recommended answer:
Yes.

Reason: confirmed outcomes are the strongest learning input because they prove whether a recommendation, decision, or review path actually worked.

---

## 974. user

Yes

---

## 975. assistant

Confirmed.

Confirmed outcome results will automatically create:

LearningEvidence

Next question:

Should confirmed negative outcomes create higher-priority learning evidence than positive outcomes?

My recommended answer:
Yes.

Reason: failed decisions, rework, unresolved BOM gaps, policy failures, and bad identity resolutions often teach the system more urgently than successful cases.

---

## 976. user

Yes

---

## 977. assistant

Confirmed.

Negative confirmed outcomes will create higher-priority LearningEvidence than positive outcomes.

Next question:

Should negative outcome LearningEvidence automatically create a follow-up RecommendationArtifact?

My recommended answer:
Yes, when severity is medium/high.

Example:

Decision outcome failed
→ Create RecommendationArtifact:
Review decision pattern and update rule/template/policy

Reason: failed outcomes should not just be stored; they should trigger improvement loops.

---

## 978. user

Yes

---

## 979. assistant

Confirmed.

This closes the learning loop correctly.

Negative Outcome Improvement Loop
DecisionArtifact
        ↓
OutcomeCheckRun
        ↓
Negative Confirmed Outcome
        ↓
High-Priority LearningEvidence
        ↓
RecommendationArtifact

Examples:

BOM Sync Decision Failed
→ Recommendation:
Review BOM synchronization process

Identity Resolution Failed
→ Recommendation:
Review matching rules

Import Override Caused Issues
→ Recommendation:
Review validation rules

Security Decision Failed
→ Recommendation:
Review policy configuration
Severity Rule
Low severity
→ LearningEvidence only

Medium severity
→ LearningEvidence
→ RecommendationArtifact

High severity
→ LearningEvidence
→ RecommendationArtifact
→ ReviewTaskArtifact (optional)
Learning Hierarchy (Now Complete)
Knowledge
      ↓
Recommendation
      ↓
Suggested Action
      ↓
Review Task
      ↓
Decision
      ↓
Outcome Check
      ↓
Confirmed Outcome
      ↓
LearningEvidence
      ↓
LearningSignalArtifact
      ↓
LearningModel
      ↓
LearningPolicy

This is the first fully closed governance-learning cycle in the platform.

Next Question

Should LearningSignalArtifact be created only from outcome patterns, or from all governance patterns?

My recommended answer:

All governance patterns.

Sources:

Repeated Recommendation Acceptance
Repeated Recommendation Rejection

Repeated Action Selection
Repeated Action Rejection

Repeated Decision Outcomes

Repeated Import Overrides

Repeated Identity Resolution Decisions

Repeated Data Quality Issues

Repeated Security Events

Repeated QueryIntent Candidates

Repeated Retrieval Fallbacks

Reason:

The platform should learn from the entire governance system, not just outcomes. Outcomes are the strongest signal, but many valuable patterns appear earlier in the lifecycle.

---

## 980. user

Yes

---

## 981. assistant

Confirmed.

LearningSignalArtifact will be generated from all governance patterns, not just outcome patterns.

Learning Signal Sources
Outcome-Based Signals (Highest Value)
Repeated successful outcomes
Repeated failed outcomes
Repeated decision reversals
Repeated override failures
Recommendation-Based Signals
Repeated recommendation acceptance
Repeated recommendation rejection
Repeated recommendation deferral
Action-Based Signals
Repeated action selection
Repeated action rejection
Repeated action superseding
Review & Decision Signals
Repeated approvals
Repeated rejections
Repeated escalations
Repeated conflicts
Data & Import Signals
Repeated import overrides
Repeated data quality issues
Repeated identity match approvals
Repeated identity match rejections
AI & Retrieval Signals
Repeated QueryIntentCandidates
Repeated fallback usage
Repeated context filtering patterns
Repeated confidence reductions
Repeated retrieval failures
Security & Governance Signals
Repeated policy violations
Repeated access requests
Repeated access denials
Repeated security events
Learning Signal Creation Rule
Single Event
    ↓
LearningEvidence

Repeated Pattern
    ↓
LearningSignalArtifact

This prevents noise while preserving all evidence.

LearningSignalArtifact Structure
LearningSignalArtifact : BaseArtifact

├── SignalType
├── SignalCategory
├── SourcePattern
├── Frequency
├── TrendDirection
├── Severity
├── Confidence
├── EvidenceReferences
├── RelatedArtifacts
├── SuggestedImprovement
├── Status
└── AuditReferences

Example:

SignalType:
IMPORT_OVERRIDE_PATTERN

Pattern:
Validation Rule XYZ overridden 87 times

Confidence:
0.96

Suggested Improvement:
Review validation rule
Next Question

Should LearningSignalArtifact automatically create a RecommendationArtifact when confidence exceeds a threshold?

My recommended answer:

Yes.

Example:

Learning Signal:
Identity Rule ABC rejected 120 times

Confidence:
0.97

Result:
Automatically create RecommendationArtifact

"Review Identity Matching Rule ABC"

Reason:

Otherwise signals become passive observations. High-confidence signals should initiate governance action and feed continuous improvement.

---

## 982. user

Yes

---

## 983. assistant

Confirmed.

High-confidence LearningSignalArtifact can automatically create a RecommendationArtifact.

Rule:

LearningSignalArtifact
Confidence >= threshold
Severity >= threshold
→ RecommendationArtifact

Example:

Learning Signal:
Identity rule repeatedly rejected

→ Recommendation:
Review identity matching rule

Next question:

Should the threshold for converting LearningSignals into Recommendations be platform-defined in MVP?

My recommended answer:
Yes.

MVP:

Platform-defined thresholds
Tenant override placeholder only

Reason: learning automation affects governance flow, so MVP should keep thresholds consistent and explainable before allowing tenant customization.

---

## 984. user

Yes

---

## 985. assistant

Confirmed.

MVP learning automation:

LearningSignalArtifact
→ Platform-defined thresholds

Tenant customization
→ Future placeholder

Example MVP logic:

Confidence >= 0.90
AND Frequency >= threshold
AND Severity >= Medium

→ Auto-create RecommendationArtifact

This keeps learning behavior:

Predictable
Auditable
Explainable
Governed

while avoiding tenant-specific tuning complexity in early releases.

Milestone 4 Status

We have now defined a complete governance and learning loop:

RecommendationArtifact
    ↓
SuggestedAction
    ↓
ReviewTaskArtifact
    ↓
DecisionArtifact
    ↓
OutcomeCheckRun
    ↓
LearningEvidence
    ↓
LearningSignalArtifact
    ↓
RecommendationArtifact

This is the first fully closed-loop intelligence cycle of the platform.

Next Question

Should Milestone 4 include a dedicated Decision Explorer and Governance Dashboard, or should decisions be viewed only through the generic Artifact Explorer initially?

My recommended answer:

Include a basic Decision Explorer and Governance Dashboard in Milestone 4.

Decision Explorer:

Recommendations
Review Tasks
Decisions
Outcomes
Learning Signals

Governance Dashboard:

Open Reviews
Pending Decisions
Blocked Decisions
Escalations
Outcome Success Rate
Learning Signals

Reason:

Milestone 4 is fundamentally about governance and decision intelligence. These concepts deserve first-class visibility rather than being buried inside the generic Artifact Explorer.

---

## 986. user

Yes

---

## 987. assistant

Confirmed.

Milestone 4 will include dedicated governance visibility.

Decision Explorer

Purpose:

Explore governance activity
Trace decisions
Review outcomes
Understand learning patterns

Views:

Recommendations
Review Tasks
Decisions
Outcome Checks
Learning Signals

Filters:

Status
Priority
Severity
Decision Type
Recommendation Type
Owner
Approver
Date Range
Confidence
Governance Dashboard

Purpose:

Governance health
Decision effectiveness
Review workload
Learning trends

Initial widgets:

Open Review Tasks
Pending Decisions
Blocked Decisions
Escalated Decisions
Decision Throughput
Outcome Success Rate
Decision Reversal Rate
Top Data Quality Issues
Top Learning Signals
High-Risk Recommendations
Relationship Integration

Dashboard items should drill into:

Decision Explorer
360° Context View
AI Trace
Governance Flow View
Artifact Explorer

This keeps governance as a first-class capability rather than hiding it inside generic artifact browsing.

Next Question

Should Milestone 4 include governance KPIs and trend analytics, or only operational dashboards?

My recommended answer:

Include basic governance KPIs in Milestone 4.

Examples:

Decision Success Rate
Decision Failure Rate
Average Review Time
Escalation Rate
Recommendation Acceptance Rate
Recommendation Rejection Rate
Outcome Verification Rate
Learning Signal Generation Rate

Reason:

Milestone 4 is where the platform becomes a decision intelligence system. Without KPIs, users can see decisions but cannot measure governance effectiveness over time.

---

## 988. user

Yes

---

## 989. assistant

Confirmed.

Milestone 4 will include basic governance KPIs and trend analytics.

Initial KPI set:

Decision Success Rate
Decision Failure Rate
Average Review Time
Escalation Rate
Recommendation Acceptance Rate
Recommendation Rejection Rate
Outcome Verification Rate
Learning Signal Generation Rate
Blocked Decision Count
High-Risk Recommendation Count

Next question:

Should KPI calculations be based on fixed platform formulas in MVP?

My recommended answer:
Yes.

Use platform-defined KPI formulas first, with tenant-configurable KPI definitions as a future extension.

Reason: governance analytics must be consistent and trusted before allowing customization.

---

## 990. user

Yes

---

## 991. assistant

Confirmed.

Milestone 4 KPI formulas will be platform-defined in MVP.

Future placeholder:

CustomKpiDefinitionVersion : BaseArtifact

Next question:

Should governance KPIs be calculated from SQL operational records, Meta Graph relationships, or both?

My recommended answer:
Use both.

SQL
→ counts, durations, timestamps, statuses, throughput

Meta Graph
→ relationship-based metrics, decision chains, dependency impact, learning paths

Reason: SQL is better for operational aggregation, while the Meta Graph is better for understanding connected governance flows.

---

## 992. user

Both

---

## 993. assistant

Confirmed.

Governance KPI calculation will use both SQL and Meta Graph.

KPI Computation Architecture
SQL (Operational Analytics)

Best for:

Counts
Durations
SLA metrics
Review times
Decision throughput
Escalation counts
Acceptance rates
Rejection rates
Outcome verification rates

Examples:

Average Review Time
Decision Success Rate
Decision Failure Rate
Escalation Rate
Recommendation Acceptance Rate
Meta Graph (Relationship Analytics)

Best for:

Decision chains
Governance dependencies
Learning propagation
Artifact impact analysis
Recommendation influence
Decision lineage
Cross-artifact governance flow

Examples:

Most influential recommendations
Decision dependency depth
Learning signal propagation
Governance bottlenecks
Artifact impact chains
Unified KPI Layer

Future architecture:

GovernanceAnalyticsService
├── SQL Metrics Provider
├── Graph Metrics Provider
└── KPI Aggregation Engine

This prevents dashboards from directly querying SQL or Memgraph.

Milestone 4 Status

Milestone 4 now contains:

RecommendationArtifact
SuggestedAction

ReviewTaskArtifact
Review Templates

DecisionArtifact
Approval Rules
Escalation Rules

OutcomeCheckRun

LearningEvidence
LearningSignalArtifact

Decision Explorer
Governance Dashboard

Governance KPIs
Trend Analytics

At this point, Milestone 4 is largely complete architecturally.

Next Question (Transition to Milestone 5)

Before implementing agents and workflows, should we first create a ToolDefinitionVersion framework and Tool Registry?

My recommended answer:

Yes.

Because agents and workflows should not directly know about tools.

Architecture:

ToolDefinitionVersion : BaseArtifact
ToolDefinitionVersion
├── Name
├── Description
├── ToolCategory
├── InputSchema
├── OutputSchema
├── Permissions
├── ClassificationRules
├── RiskLevel
├── TimeoutRules
├── FallbackRules
├── Version
└── Status

Then:

Agent
    ↓
Tool Registry
    ↓
ToolDefinitionVersion
    ↓
Tool Gateway
    ↓
Tool Execution

Reason:

Agents, workflows, governance, permissions, auditing, and future action connectors all depend on a governed tool catalog.

Without this, agent implementation becomes tightly coupled to tool implementations.

Do you agree that ToolDefinitionVersion and Tool Registry should be the first component of Milestone 5 before AgentVersion and WorkflowVersion?

---

## 994. user

Yes

---

## 995. assistant

Confirmed.

Milestone 5 starts with:

ToolDefinitionVersion
Tool Registry
Tool Gateway foundation

before implementing agents and workflows.

Next question:

Should tools be grouped into tool categories from day one?

My recommended answer:
Yes.

Initial categories:

Graph Tools
Document Tools
Vector Search Tools
Context Assembly Tools
Artifact Tools
Governance Tools
Analytics Tools
Notification Tools
Connector Tools
Future Action Tools

Reason: categories help with permissions, risk scoring, discovery, agent capability limits, and admin review.

---

## 996. user

Yes

---

## 997. assistant

Confirmed.

Initial tool categories:

Graph Tools
Document Tools
Vector Search Tools
Context Assembly Tools
Artifact Tools
Governance Tools
Analytics Tools
Notification Tools
Connector Tools
Future Action Tools

Next question:

Should each ToolDefinitionVersion declare whether it is read-only, write-capable, or action-capable?

My recommended answer:
Yes.

Use capability flags:

ReadOnly
CreatesPlatformArtifact
CreatesReviewTask
ModifiesPlatformMetadata
CallsExternalSystem
WritesExternalSystem
RequiresApproval
SupportsDryRun

Reason: tool capability directly affects agent trust levels, workflow safety, risk scoring, approval requirements, and MVP read-only boundaries.

---

## 998. user

Yes

---

## 999. assistant

Confirmed.

This is a critical governance decision for Milestone 5.

ToolDefinitionVersion Capability Model

Each tool declares its capabilities explicitly:

ToolDefinitionVersion : BaseArtifact

├── Name
├── Description
├── ToolCategory
├── InputSchema
├── OutputSchema

├── ReadOnly
├── CreatesPlatformArtifact
├── ModifiesPlatformMetadata

├── CreatesReviewTask
├── CreatesDecision

├── CallsExternalSystem
├── WritesExternalSystem

├── RequiresApproval
├── SupportsDryRun

├── RiskLevel
├── Permissions
├── ClassificationRules

├── TimeoutRules
├── FallbackRules

└── Version
Examples
Graph Search Tool
ReadOnly = true
CallsExternalSystem = false
WritesExternalSystem = false
RequiresApproval = false
RiskLevel = Low
Recommendation Creation Tool
ReadOnly = false
CreatesPlatformArtifact = true
CreatesReviewTask = false
RequiresApproval = false
RiskLevel = Medium
Future ERP Write Tool
CallsExternalSystem = true
WritesExternalSystem = true
RequiresApproval = true
SupportsDryRun = true
RiskLevel = High
Benefits
Agent Safety

Agents can reason:

Can I use this tool?
What risk does it introduce?
Do I need approval?
Governance
Tool Selection
Approval Requirements
Audit Rules
Execution Rules

become metadata-driven.

Future Enterprise Actions

When we enable actions later:

ToolDefinitionVersion
→ already contains action governance metadata

without redesign.

Next Question

Should ToolDefinitionVersion also declare which QueryIntent categories are allowed to use the tool?

Example:

Graph Search Tool
→ Allowed:
Find Part
Find Document
Impact Analysis

Not Allowed:
Security Investigation

My recommended answer:

Yes.

Add:

AllowedIntentCategories
AllowedIntentIds

Reason:

This gives another layer of governance:

Intent
    ↓
Allowed Tool
    ↓
Allowed Execution

instead of allowing any intent to invoke any tool.

---

## 1000. user

Yes