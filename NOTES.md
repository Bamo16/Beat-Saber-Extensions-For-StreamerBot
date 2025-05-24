
# Beat Saber Extensions Edge Cases

* Add specific error message for attempting to `!bsrbump` a song when it it already up next (perform no action)
* When Raid Requestor makes a request using !bsr, test if queue is open
  * If it's closed, verify bsr id via beatsaver API and follow !att workflow
* For raid requests, handle remaps, banned mappers etc.
* Regarding remaps:
  * WHen a request is made and it's for a remap, the bot will respond indicating the actual BSR ID that was added
  * BS+ will only follow the first remap and will not continue recursively
  * BS+ does not care about the blacklisted/allowed status of the originally requested BSR ID, but will reject when the remapped value is blacklisted
* Regarding history:
  * Not sure how/when it resets, but if a song appears in history, !bsrs will be rejected for "this song was already requested this session!"
