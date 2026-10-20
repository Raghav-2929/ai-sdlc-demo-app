# Feature Specification: Approval Limits by Role

**Feature Branch**: `feature/role-approval-limits`

**Created**: 2026-10-20

**Status**: Clarified and analysed (`/speckit-analyze` findings resolved, see [analysis.md](analysis.md)); ready for implementation

**Input**: User description: "Add approval limits by role to the invoice approval service. A Manager may approve invoices up to 50,000. A Director may approve invoices up to 500,000. Anything above 500,000 needs two distinct approvers, and the second approver must be a director. The same user may never count as both approvers on one invoice. A rejected attempt returns a clear reason, and nothing about the invoice changes."

## Glossary

- **Refused** (code: "denied"): an approval attempt stopped by an approval rule. The invoice does not change.
- **Rejected**: the existing *Reject* decision a person takes on an invoice, with a reason. Not the same as a refused approval.
- **Final approval**: the approval that moves an invoice to Approved. **First approval**: the first of two approvals on an invoice above 500,000.00; it does not approve the invoice.
- **Approver**: the signed-in person acting on an invoice (username and role). Not the retired role named "Approver".

## Clarifications

### Session 2026-10-20

Questions raised by `/speckit-clarify`. The brief did not answer them, so the developer chose the answer and the reason below. Each one is listed in the pull request for the product owner to confirm.

- Q: Is "up to 50,000" / "up to 500,000" inclusive? → A: Yes. 50,000.00 is within a Manager's limit; 50,000.01 is not. 500,000.00 is within a Director's single-approval limit; 500,000.01 needs two approvers.
- Q: Above 500,000, who may give the **first** approval? → A: Anyone with an approval role (Manager or Director). The brief constrains only the second approver ("the second approver must be a director"); requiring two Directors would make that sentence redundant. **Consequence, stated on purpose:** a Manager cannot give the *final* approval above 50,000.00, but may give the *first* of two approvals above 500,000.00.
- Q: What happens to the existing generic "Approver" role and to Admin? → A: The generic Approver role is retired and replaced by Manager and Director, which carry limits. Admin has no approval limit, so Admin cannot approve (fail closed) and gets a reason saying so.
- Q: Do the limits also restrict **rejecting** an invoice? → A: No. The limits govern approval only. Managers and Directors may reject any pending invoice with a reason, and Admin keeps the right to reject that it has today.
- Q: After a first approval above 500,000, what status does the invoice have? → A: It stays Pending Approval, with the first approver and time recorded, so it still appears in the approvals queue for the second approver.

### Session 2026-10-20 (follow-up after `/speckit-analyze`)

- Q: Before the two-approver rule ships (User Story 1 on its own), what happens above 500,000.00? → A: Every approval is refused with the reason "… above the Director limit of 500,000.00. Invoices above 500,000.00 need two approvers." User Story 2 replaces this with the two-approver flow.
- Q: When more than one rule refuses an attempt, which reason is shown? → A: The first that applies, in this order: (1) the role cannot approve; (2) the same person already gave the first approval; (3) the second approval is not from a Director; (4) the amount is above the approver's limit.
- Q: Are role names matched exactly? → A: Yes. Roles come from configuration and must match `Manager` or `Director` exactly; anything else cannot approve. Usernames, by contrast, ignore case and surrounding spaces (FR-004).
- Q: Does the first-approval record survive a later decision? → A: Yes. `first approved by / at` stays on the invoice after it is Approved or Rejected, as history.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Approvals stay within each role's limit (Priority: P1)

A Manager or Director opens the approvals queue and approves an invoice. If the amount is within their role's limit, the invoice is approved. If it is not, the approval is refused with a reason that says which limit applies and who can approve instead, and the invoice is left exactly as it was.

**Why this priority**: This is the core spending control. Without it any approver can approve any amount, which is the risk the feature exists to remove. It is valuable on its own even before the two-approver rule exists, because it already stops Managers from approving large invoices.

**Independent Test**: Approve invoices at, just above and well above each limit as a Manager and as a Director; check which are approved, which are refused, the refusal reason, and that refused invoices are unchanged.

**Interim behaviour (until User Story 2 ships)**: an invoice above 500,000.00 is refused for every role with the reason "Invoice … is …, which is above the Director limit of 500,000.00. Invoices above 500,000.00 need two approvers." (Manager: "… above the Manager limit of 50,000.00. Invoices above 500,000.00 need two approvers.")

**Acceptance Scenarios**:

1. **Given** a pending invoice of 50,000.00, **When** a Manager approves it, **Then** it is Approved and records the Manager as the decider. *(AL-1)*
2. **Given** a pending invoice of 50,000.01, **When** a Manager approves it, **Then** the approval is refused with a reason naming the Manager limit of 50,000.00 and that a Director must approve it, and the invoice is unchanged. *(AL-1, AL-5)*
3. **Given** a pending invoice of 120,000.00, **When** a Manager approves it, **Then** it is refused; **When** a Director then approves it, **Then** it is Approved. *(AL-1, AL-2)*
4. **Given** a pending invoice of 500,000.00, **When** a Manager approves it, **Then** it is refused; **When** a Director approves it, **Then** it is Approved with a single approval. *(AL-1, AL-2)*
5. **Given** a pending invoice of 100.00, **When** a user whose role has no approval limit (Admin, or any unknown role) approves it, **Then** it is refused with a reason that the role cannot approve and a Manager or Director must, and the invoice is unchanged. *(AL-6, AL-5)*
6. **Given** a pending invoice of 750,000.00, **When** a Manager rejects it with a reason, **Then** it is Rejected (limits do not apply to rejecting). *(FR-009)*

---

### User Story 2 - Two different people approve anything above 500,000 (Priority: P2)

For an invoice above 500,000, the first approval is recorded but does not complete the approval. A second, different person who is a Director completes it. The same person can never be both approvers, however their name is typed.

**Why this priority**: It closes the remaining gap after Story 1 (very large invoices) with dual control. It depends on Story 1's limits and refusal behaviour, so it comes second.

**Independent Test**: Take one invoice above 500,000 through every first/second approver combination; check when it becomes Approved, when it stays pending, and that every refused attempt leaves it unchanged.

**Acceptance Scenarios**:

1. **Given** a pending invoice of 750,000.00 with no approvals, **When** Director A approves it, **Then** it stays Pending Approval and records A as first approver. *(AL-3)*
2. **Given** that first approval by Director A, **When** Director B approves it, **Then** it is Approved, records B as the decider, and still shows A as first approver. *(AL-3)*
3. **Given** a pending invoice of 500,000.01 with no approvals, **When** Manager M approves it, **Then** M is recorded as first approver; **When** Director B then approves it, **Then** it is Approved. *(AL-3)*
4. **Given** a first approval by Director A, **When** A approves again, written with any letter case or surrounding spaces, **Then** it is refused because the same person cannot give both approvals, and the invoice is unchanged. *(AL-4, AL-5)*
5. **Given** a first approval by Manager M, **When** M approves again, **Then** it is refused for being the same person (that reason wins over "not a Director"), and the invoice is unchanged. *(AL-4, AL-5)*
6. **Given** a first approval by Director A, **When** Manager M approves, **Then** it is refused because the second approval must come from a Director, and the invoice is unchanged. *(AL-3, AL-5)*
7. **Given** a pending invoice of 500,000.01, **When** a Director approves it, **Then** it only records the first approval; it is not Approved yet. *(AL-2, AL-3)*
8. **Given** a pending invoice of 750,000.00 with or without a first approval, **When** an Admin approves it, **Then** it is refused because the role cannot approve, and the invoice is unchanged. *(AL-6, AL-5)*
9. **Given** a first approval by Director A, **When** anyone allowed to reject rejects it with a reason, **Then** it is Rejected and the first-approval record is kept. *(FR-009)*

---

### User Story 3 - The approver sees why an approval was refused (Priority: P3)

In the web UI, an approver who clicks Approve on an invoice they may not approve sees the reason on the same screen, and the invoice is still listed as pending. An invoice waiting for a second approval says so in the queue.

**Why this priority**: The rules already protect the business through the API after Stories 1 and 2; this story makes them understandable to the person using the screen.

**Independent Test**: Sign in as a Manager, create and submit a 75,000.00 invoice, click Approve, and check the message and the invoice status in the browser.

**Acceptance Scenarios**:

1. **Given** a Manager is signed in and a 75,000.00 invoice is pending, **When** they click Approve, **Then** the screen shows the refusal reason naming the 50,000.00 Manager limit, and the invoice is still listed as Pending Approval. *(AL-1, AL-5)*
2. **Given** an invoice above 500,000.00 has a first approval and is still pending, **When** any approver opens the queue, **Then** it shows "First approval by <user> · needs a second approval from a different Director". *(AL-3)*

---

### Edge Cases

- Exactly at each limit (50,000.00 and 500,000.00) is allowed for the role that owns it; one cent above is not.
- A Manager at exactly 500,000.00 is refused (single-approval path, above 50,000.00); at 500,000.01 a Manager may give the first approval.
- The same person typed differently (`Demo.Director`, ` demo.director `) is still the same person, whichever of the two names has the spaces or capitals.
- A Manager tries to give the second approval after a *different* person gave the first: refused (second must be a Director).
- A Director tries to give the second approval after giving the first: refused (same person), even though the role is right.
- A refused attempt must not leave a partial record behind (for example a first approver written before the refusal).
- An invoice that is not pending (draft, approved, rejected) is refused as today, with its current status in the reason.
- An invoice that does not exist returns "not found" as today.
- Rejecting an invoice (any amount) by a Manager, Director or Admin works as today.
- Two approvers clicking at the same moment on a large invoice: out of scope for this demo (single in-memory store); recorded as a known limitation in the pull request.
- Amounts with more than two decimals (e.g. 50,000.004) are compared exactly and refused above the limit, but the reason prints two decimals. Creation does not yet forbid them; recorded as a known limitation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 (AL-1)**: The system MUST allow a Manager to give the final approval on an invoice of at most 50,000.00, and MUST refuse a Manager's final approval of anything larger. Above 500,000.00 a Manager MAY give the first of the two approvals (FR-003), never the second.
- **FR-002 (AL-2)**: The system MUST allow a Director to approve, on their own, an invoice whose amount is at most 500,000.00.
- **FR-003 (AL-3)**: For an invoice above 500,000.00, the system MUST require two approvals from two different people before the invoice is Approved. The first approval MAY come from a Manager or a Director and MUST be recorded (who and when) while the invoice stays Pending Approval. The second approval MUST come from a Director.
- **FR-004 (AL-4)**: The system MUST NOT let one person count as both approvers on the same invoice. People MUST be compared by username, ignoring letter case and surrounding spaces.
- **FR-005 (AL-5)**: When an approval is refused, the system MUST return a clear reason that states which rule was broken and what is needed instead, and MUST NOT change any data on the invoice.
- **FR-006 (AL-6)**: A user whose role has no approval limit (including Admin and any unknown role) MUST NOT be able to approve, and MUST receive the reason (FR-005), not a bare "forbidden".
- **FR-007**: The approver's identity and role MUST come from the signed-in session, not from anything the client sends.
- **FR-008**: The approvals screen MUST show the refusal reason to the user, and MUST show when a pending invoice is waiting for a second approval and who gave the first.
- **FR-009**: Existing behaviour MUST stay as it is: submitting drafts, rejecting with a required reason (by Manager, Director or Admin), refusing decisions on invoices that are not pending, and "not found" for unknown invoices.
- **FR-010**: Each approval limit MUST be defined in exactly one place so it can be changed without hunting through the code.
- **FR-011**: When several rules refuse one attempt, the reason MUST name the first that applies, in the order given in the Clarifications (role, same person, Director second, limit).

### Key Entities

- **Invoice**: amount, status (Draft, Pending Approval, Approved, Rejected), who decided and when, decision note, and — new — who gave the first approval and when (only used above 500,000.00; kept after the decision).
- **Approver**: the signed-in person acting on an invoice: username and role.
- **Approval limit**: the largest amount a role may give the final approval for on its own: Manager 50,000.00, Director 500,000.00; no other role has one.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of attempts to give a *final* approval above the approver's own limit are refused, verified at every boundary in the acceptance scenarios.
- **SC-002**: 0 invoices change in any way as a result of a refused approval attempt.
- **SC-003**: No invoice above 500,000.00 can reach Approved with fewer than two different people, the last of them a Director.
- **SC-004**: Every refusal message names the rule and what is needed, so an approver can act on it without asking support; the message appears on the same screen in the same action.
- **SC-005**: All approve, reject and submit behaviour that worked before the change still works for invoices within the approver's limit.

## Assumptions

- Amounts are in a single currency with two decimal places; there is no currency conversion. Sub-cent amounts are not rejected at creation (known limitation, see Edge Cases).
- Roles come from the existing sign-in configuration and are matched exactly. Demo accounts become: `demo.manager` (Manager), `demo.director` and `demo.director2` (Director), `demo.admin` (Admin). These are demo accounts, not real credentials.
- The existing generic Approver role is retired (see Clarifications).
- Rejecting is not limited by amount (see Clarifications).
- The app keeps its single in-memory store; simultaneous approvals of the same invoice are out of scope.
- Audit history beyond "first approver" and "decider" (for example a full approvals log) is out of scope for this release.
