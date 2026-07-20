document data model, design and architecuture/implementation in markdown documents for future iteration.

plan one time seed/import from "import/12Legs Clickup Tasks and Billing - tasks.csv"

cannot deploy to lightsail at the moment. use a docker container running ubuntu24 locally to test deployment    

logging should output to jsonl format on server to log files per-day

create xunit tests for repository and api surface (aspnet test server in-memory)

combine projects in backend into a single project with namespaces, keep migrations as separate project

refactor any use of entity framework to use dapper

ability to work from agency billing panel as contractor
see outstanding work by client / project
complete work, update status in clickup
specify work as non-bill or billable, update attribute in clickup
mark work as ready to bill
email on last day of billing period, finalize billing

email agency users once a week with summary of client / project / task completed work. estimate of hours per scope / totalreco

replace this abstraction with .net TimeProvider
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
