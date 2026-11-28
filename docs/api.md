# Connext API

This file documents the FR-12 poll endpoints and SignalR events added for issue #78.
The complete API catalog requested in issue #72 remains in progress.

## Implemented limits

| Resource | Limit |
|---|---:|
| Conversation message content | 2,000 characters |
| Group members | 3–50, including the creator |
| Image uploads | 15 MiB |
| Video and document uploads | 100 MiB each |
| Multipart upload request | 110 MiB |
| Message page size | 1–50 |
| Message reactions | `Like`, `Love`, `Haha`, `Wow`, `Sad`, `Angry` |
| Message recall | Within 24 hours of sending |
| Pinned messages | Up to 5 per conversation |
| Password reset OTP | Expires after 3 minutes; 5 failed attempts lock OTP sending for 30 minutes |
| Re-sending a declined friend request | Wait 24 hours |

In group messages, `@all` mentions every other group member.

## Group polls (FR-12)

All endpoints require a valid JWT bearer token. Polls are available only in group
conversations, and every request is scoped to a conversation the caller belongs to.

| Method | Endpoint | Request | Success response | Errors |
|---|---|---|---|---|
| `POST` | `/api/conversations/{conversationId}/polls` | `{"question":"Where should we meet?","options":["Cafe","Library"]}` | `201 Created`, `PollResponseDto` | `400` invalid question/options or conversation; `401` missing/invalid JWT |
| `GET` | `/api/conversations/{conversationId}/polls/{pollId}` | — | `200 OK`, `PollResponseDto` | `404` poll or group unavailable to caller; `401` missing/invalid JWT |
| `POST` | `/api/conversations/{conversationId}/polls/{pollId}/vote` | `{"optionId":"<option-guid>"}` | `200 OK`, updated `PollResponseDto` | `400` option does not belong to poll or conversation invalid; `401` missing/invalid JWT |

Questions must contain 1–200 characters. A poll must have 2–10 non-empty,
distinct options. A member can have one vote per poll and can change that vote.

`PollResponseDto` contains the poll and message IDs, question, creation time,
option IDs/text, each option's vote count and percentage, total votes, and
`myVoteOptionId` (null until the caller votes). The poll is also included in the
conversation's regular message list as a message with type `Poll`.

Example response:

```json
{
  "id": "poll-guid",
  "conversationId": "conversation-guid",
  "messageId": "message-guid",
  "question": "Where should we meet?",
  "createdAt": "2026-11-27T14:00:00Z",
  "totalVotes": 1,
  "myVoteOptionId": null,
  "options": [
    { "id": "option-guid", "text": "Cafe", "voteCount": 1, "percentage": 100.00 },
    { "id": "option-guid-2", "text": "Library", "voteCount": 0, "percentage": 0 }
  ]
}
```

### SignalR

Events are sent to the conversation room, which contains active connections of
group members:

| Event | Payload | When |
|---|---|---|
| `PollCreated` | `MessageResponseDto` with `type: 4` (`MessageType.Poll`) and a populated `poll` | A member creates a poll |
| `PollVoted` | `{ "poll": PollResponseDto, "userId": "...", "optionId": "..." }` | A member votes or changes their vote |

`PollVoted` carries shared counts and percentages plus the voter and selected
option IDs. `myVoteOptionId` is excluded from that group broadcast because it is
caller-specific; the voter receives it in the HTTP response, and clients can
refresh it with the GET endpoint.
