# Output streams and CI configuration

[Back to the README](../README.md)

## Standard output vs. standard error

By default the CLI splits its output as follows:

* **stdout** carries the command's result: machine-readable payloads such as the JSON emitted by `submission get`, `apps get` and `submission rollout get`, the package path printed by `package`, and `--help` text. This keeps `msstore submission get ... | ConvertFrom-Json` and `$(msstore package ...)` reliable.
* **stderr** carries everything else meant for a human: progress, status, success messages, tables, prompts and verbose logging.

`--output-stream stdout` deliberately breaks that separation: it moves the human-readable half onto stdout, where it is interleaved with any payload.

Two things sit outside the option's scope on purpose, matching the behavior of other CLIs:

* Machine-readable payloads are always written to stdout, so they are never affected by the option.
* `--help` is always written to stdout, so that `msstore --help | more` works, and command line parse errors are always written to stderr, because they accompany a non-zero exit code.

## Migrating scripts

> [!WARNING]
> **Behavior change.** The `apps list`, `flights list` and `info` tables, the interactive prompts and
> the browser confirmation used to go to stdout. They now go to stderr with everything else meant for
> a human. Scripts that piped or captured them, such as `msstore apps list | grep ...` or
> `msstore info > file`, will see nothing on stdout, with a zero exit code and no diagnostic.
>
> Either of these restores a working script:
>
> * `2>&1`, to merge the two streams, or
> * `--output-stream stdout`, or `MSSTORE_OUTPUT_STREAM=stdout` for a whole job, which puts **all**
>   human-readable output on stdout. Note that this is not the old routing: progress, status and
>   verbose logging already went to stderr before this change, so a script will now also receive that
>   text alongside the table it was after.
>
> Machine-readable payloads (`submission get`, `apps get`, `package`) were already on stdout and
> remain there. Merging streams or selecting stdout for human-readable output mixes that text into
> the captured payload; keep the default separation when parsing results.

## Azure DevOps

Some Azure DevOps task configurations surface stderr lines as `##[error]`, even when the command succeeds. Whether this also fails a task depends on its task type and settings, including `failOnStderr`. Human-readable progress on stderr does not by itself mean the CLI failed; check the command's exit code.

If your task treats stderr as errors, move the human-readable output to stdout:

```yaml
- script: msstore publish .\MyApp --output-stream stdout
  displayName: Publish to the Microsoft Store
```

This example uses a Windows path; use a path appropriate for your build agent.

Or set it once for a whole job, so that every `msstore` call picks it up:

```yaml
variables:
  MSSTORE_OUTPUT_STREAM: stdout
```

> [!IMPORTANT]
> Machine-readable payloads always go to stdout. When `MSSTORE_OUTPUT_STREAM` is set for a whole job, the human-readable output is interleaved with the payload, which breaks capturing it. Pass `--output-stream stderr` on those specific calls to opt back out: the option always overrides the environment variable.
>
> ```yaml
> variables:
>   MSSTORE_OUTPUT_STREAM: stdout
>
> steps:
> - script: msstore publish .\MyApp                                   # human-readable output on stdout
> - script: msstore submission get $(AppId) --output-stream stderr    # clean JSON on stdout
> ```

## GitHub Actions

No output-routing change is normally needed. A standard `run` step uses the shell's exit status to determine failure; ordinary stderr text is not automatically an error annotation. The default separation is appropriate for both human-readable logs and captured results.
