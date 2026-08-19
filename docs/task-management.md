# Task Management

## 1. Purpose

This document defines how implementation work is created, prioritized, selected, executed, reviewed, and completed. Tasks are repository-native Markdown documents so their intent, dependencies, acceptance criteria, and history remain versioned with the code.

The canonical delivery stages are defined in [Implementation Plan](implementation-plan.md). Task documents decompose those stages into bounded executable work.

## 2. Storage Layout

```text
docs/
    tasks/
        stage-1.md
        stage-1/
            S1-001-write-stage-1-gherkin.md
            S1-002-initialize-solution.md
            ...
        stage-2.md
        stage-2/
            ...
```

Each stage index lists its ordered backlog, current status, priority, and dependencies. Each executable task has one Markdown document with YAML front matter.

The stage index is a projection for navigation. The individual task document is authoritative for task metadata and completion evidence.

## 3. Task Metadata

Every task document begins with YAML front matter using this schema:

```yaml
---
schema_version: 1
id: S1-001
title: Write Stage 1 executable Gherkin specifications
stage: 1
status: ready
priority: 1000
type: acceptance
depends_on: []
labels:
  - bdd
  - planning
created: 2026-08-19
updated: 2026-08-19
---
```

Fields:

| Field | Required | Meaning |
| --- | --- | --- |
| `schema_version` | Yes | Metadata schema version; initially `1` |
| `id` | Yes | Stable task ID, never reused |
| `title` | Yes | Short imperative outcome |
| `stage` | Yes | Delivery stage number |
| `status` | Yes | Current workflow state |
| `priority` | Yes | Integer priority; higher values are selected first |
| `type` | Yes | Work category such as `acceptance`, `feature`, `infrastructure`, `test`, `documentation`, `defect`, or `decision` |
| `depends_on` | Yes | Task IDs that must be `done` first |
| `labels` | Yes | Searchable technical or domain tags; may be empty |
| `created` | Yes | Creation date in `YYYY-MM-DD` |
| `updated` | Yes | Last material metadata or scope change |

Optional fields may include:

```yaml
owner: null
blocked_reason: null
supersedes: null
adr: null
```

Do not store volatile prose, acceptance results, or long explanations in YAML. Those belong in the document body.

## 4. Task IDs

Stage task IDs use `S<stage>-<sequence>`:

```text
S1-001
S1-002
S2-001
```

Sequence numbers identify tasks; they do not determine execution order. Priority and dependencies determine execution order. IDs are never renumbered when a task is inserted near the top of the backlog.

Newly discovered work receives the next unused sequence number even if its priority places it ahead of earlier tasks.

## 5. Priority Model

Priority is an integer where a larger value means the task should be considered sooner.

Recommended bands:

| Range | Meaning |
| --- | --- |
| `1000+` | Stage gate, critical safety issue, or release blocker |
| `900-999` | Immediate prerequisite or urgent injected work |
| `700-899` | Normal stage implementation |
| `500-699` | Supporting work that may follow core implementation |
| `300-499` | Improvement that is useful but not stage-blocking |
| `<300` | Deferred or exploratory work |

Selection order is:

1. Only consider tasks whose dependencies are `done` and whose status is `ready`.
2. Select the highest `priority`.
3. Break a tie by the earliest `created` date.
4. Break any remaining tie by task ID.

Dependencies always outrank priority. A priority `1000` task with an incomplete dependency is not ready.

### Injecting Work Near the Top

To inject a task near the top of the backlog:

1. Give it the next unused task ID.
2. Declare its real dependencies.
3. Assign a priority above the currently queued work, normally `900-999`.
4. Explain the urgency and displaced work in the task body.
5. Update the stage index.

Do not falsify dependencies to make a task appear ready. If urgent work requires changing a dependency, record the architectural reason and update both tasks.

An in-progress task is not interrupted merely because a higher-priority task was added. Interrupt only when the new task addresses a critical safety, security, financial-integrity, data-loss, or build-blocking issue and the current work can be left in a safe, documented state.

## 6. Status Workflow

```text
planned -> ready -> in_progress -> review -> done
                         └-------> blocked
blocked -> ready
```

Definitions:

- `planned`: identified, but not sufficiently specified or not eligible for execution.
- `ready`: actionable, acceptance criteria are clear, and every dependency is done.
- `in_progress`: actively being implemented; normally only one task per worker.
- `review`: implementation is complete and awaits validation or review.
- `done`: all acceptance criteria and validation requirements pass.
- `blocked`: progress requires an external decision, permission, dependency, or unavailable system.

Changing status updates the `updated` date and the stage index.

## 7. First Task in Every Stage

The first task in every stage must define or refine that stage's executable Gherkin specifications.

That task:

- Converts stage acceptance criteria into business-readable features and scenarios.
- Identifies missing domain language or behavioral decisions.
- Tags scenarios by platform and infrastructure requirements.
- Establishes traceability between stage criteria and scenarios.
- Avoids UI click details except in explicitly WPF-specific features.
- May initially leave step definitions unimplemented while the production capability does not exist.

The first task is complete when the feature files are syntactically valid, reviewed against every stage criterion, and placed in the appropriate acceptance-test structure. The stage itself is complete only when all required scenarios execute and pass.

## 8. Task Document Body

Every task document uses these sections:

```markdown
# S1-001: Task title

## Objective

One concrete outcome.

## Context

Why the task exists and relevant architectural references.

## Scope

- Included work.

## Out of Scope

- Explicitly excluded neighboring work.

## Acceptance Criteria

- Objective, observable completion conditions.

## Validation

Commands, tests, inspections, or demonstrations required.

## Completion Notes

Filled in when the task enters review or done.
```

Acceptance criteria must be verifiable. Avoid criteria such as “clean,” “good,” “proper,” or “complete” without defining observable evidence.

## 9. Creating a Task

1. Confirm the work is not already represented by an existing task.
2. Identify the smallest coherent outcome.
3. Assign the next unused ID for the stage.
4. Add complete YAML metadata.
5. State the objective and context.
6. Define included and excluded scope.
7. Declare real dependencies.
8. Choose priority from business urgency and sequencing needs.
9. Write objective acceptance criteria and validation steps.
10. Add the task to the stage index.
11. Set status to `ready` only when dependencies are done and no material decision is missing; otherwise use `planned` or `blocked`.

Tasks should normally fit one focused implementation session and produce one reviewable outcome. Split a task when it combines unrelated layers, has independently useful intermediate outcomes, or cannot be validated with a coherent test set.

## 10. Selecting and Starting a Task

1. Refresh the stage index from task metadata.
2. Filter to `ready` tasks with completed dependencies.
3. Select using the priority ordering rules.
4. Read the complete task and referenced architecture before editing code.
5. Confirm the stated validation is runnable or identify a task defect.
6. Set status to `in_progress`, assign an owner when applicable, and update the date.
7. Work only within scope unless a safety or correctness dependency is discovered.

If the task specification is materially incomplete, move it to `blocked` or `planned` and create the necessary decision task rather than guessing across an architectural boundary.

## 11. Actioning a Task

During implementation:

- Add or update tests with production behavior.
- Preserve unrelated user changes.
- Keep the change set focused on the task objective.
- Record newly discovered work as separate tasks.
- Create an ADR task when the work requires a durable architectural choice.
- Do not silently weaken acceptance criteria because implementation is difficult.
- Update documentation that would otherwise become false.

Small discoveries clearly required by the task may be included and recorded in Completion Notes. Materially separate work gets a new task and priority.

## 12. Review and Completion

Move a task to `review` when implementation is complete and all validation has been attempted. Completion Notes must include:

- What changed.
- Tests and validation commands run.
- Results.
- Deviations from the original scope.
- Follow-up task IDs.
- ADRs created or changed.

Move to `done` only when:

- Every acceptance criterion passes.
- Required tests pass in applicable environments.
- No unresolved failure is hidden by skipping, retrying indefinitely, or weakening assertions.
- Documentation and stage index are current.
- Follow-up work is separately captured.

## 13. Blocking and Replanning

A blocked task records `blocked_reason` in metadata and explains:

- The exact blocking condition.
- Evidence that established the block.
- The decision, permission, task, or external state required.
- Work that remains safe to perform, if any.

When a task changes materially, update `updated`, scope, acceptance criteria, dependencies, and stage index. If the original objective is no longer valid, mark it superseded through metadata and create a new task rather than rewriting history ambiguously.

## 14. Stage Index

Each `stage-N.md` index contains:

- Stage goal and link to the implementation plan.
- Stage-wide exit criteria.
- Ordered backlog table.
- Current next task according to priority and dependencies.
- Completion summary when the stage closes.

Example backlog table:

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| `S1-001` | Write Stage 1 Gherkin | Ready | 1000 | — |
| `S1-002` | Initialize solution | Planned | 900 | `S1-001` |

The table is updated whenever task metadata changes. If the index and task metadata disagree, task metadata wins and the index must be corrected.

## 15. Stage Completion

A stage closes only when:

- Every stage-blocking task is `done`.
- Every stage acceptance criterion in the implementation plan is satisfied.
- All required Reqnroll BDD scenarios run and pass on applicable platforms.
- CI and migration gates pass.
- A Stage Review Record is completed.
- Remaining non-blocking work is explicitly moved to a later stage rather than left ambiguous.

## 16. Future GitHub Integration

When the repository is hosted, each task may be mirrored to a GitHub Issue and project board. The Markdown task remains the durable specification; the issue may provide assignment, discussion, pull-request linkage, and automation.

Issue synchronization must preserve task ID, status, priority, dependencies, and acceptance criteria. Closing an issue does not by itself mark the repository task `done` unless the completion rules above are satisfied.
