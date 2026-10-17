# ApexPay 💸

*A digital wallet and payments API, built around one question: what happens when things go wrong?*

[Live app](https://apexpay-8ty.pages.dev) · [Live API](https://apexpay-zqfr.onrender.com) · .NET 10 · PostgreSQL · React 19

> ⏳ The API sleeps on Render's free tier. The first request after a quiet spell can take up to a minute. Give it a moment, then it's quick.

---

## Why I built it

Most practice projects are "create a row, read it back." I wanted one where the hard part is **not losing money**.

ApexPay lets a person open an account, keep money in several wallets, send it to someone, pay a merchant, and get a refund. Every naira that moves is written to a ledger, so any balance can be proved from its history.

## What you can do 🧭

- 🔐 Sign up, log in, and reset a forgotten password
- 👛 Hold several named wallets, each with its own 10-digit account number
- ➕ Add money to a wallet
- 📤 Send money by account number
- 🛍️ Pay a merchant, then 🔁 get a full refund
- 📒 Read a wallet's history, with a built-in check that the balance is right
- 🗂️ See every wallet's activity in one list, filter it down to a single wallet, and copy a reference to refund it

## Five questions that shaped everything 🧩

**1. What if the same request arrives twice?**
A flaky network, a double-click. Idempotency was a word I had never used when I started. Now every money-moving request carries an `Idempotency-Key`. The server saves a receipt for the key, and a retry gets the original answer back instead of a second charge.
If two copies arrive at the same instant, the database decides who wins, and the loser waits a few seconds for the winner's answer. If the server dies mid-request, a stuck key can be taken over after two minutes by exactly one caller.

**2. Can money appear or vanish?**
Not if every movement is double-entry. A transfer writes one debit and one credit of the same amount. A wallet's history shows its stored balance next to credits minus debits, and an `isConsistent` flag tells you if they ever disagree.

```
Transfer 2,500   wallet A ──debit──▶ ledger ◀──credit── wallet B
```

**3. What if two people touch the same wallet at once?**
"Check the balance, then subtract" has a gap in the middle. So it's one SQL statement with every rule in its `WHERE`: owner, active, enough money. When two wallets are involved, they're always updated in the same order, so opposite transfers can't deadlock.

**4. What if my own code has a bug?**
Then let the database say no. Balances can't go below zero, amounts must be positive, and emails, account numbers and references are unique. If my code slips, the database refuses to save the damage.

**The feature I cut, then brought back.** Password reset once sat in the graveyard. Sending email wants a verified domain, and the cheap senders reject consumer addresses like Gmail's. I switched it back on over SMTP, tucked behind one small `EmailSender`, with the reset token stored hashed and good for 15 minutes. Without a verified domain, delivery to any inbox is best-effort.

## How it's put together 🏗️

```
Controllers   HTTP only: routes, token, light validation
Services      the rules and transactions, behind interfaces
Database      the last line of defence
```

| | |
|---|---|
| API | ASP.NET Core (.NET 10), EF Core, Npgsql |
| Data | PostgreSQL on Neon, snake_case, all money as `numeric(18,2)` |
| Auth | JWT bearer tokens, BCrypt passwords, rate-limited sign-up and login |
| Web | React 19, Vite, TypeScript, token in `sessionStorage`, 30-minute idle logout |
| Hosting | Render (Docker) for the API, Cloudflare Pages for the web app |

Business errors are small named exceptions, each tied to one status code. The mapping lives in one place, and a test pins it.

## Does it hold up? 🧪

About 51 integration tests run the real API against a real PostgreSQL database. They cover funding with retries, transfers, checkout, refunds, the refund wallet, amount rules, login, password reset, the rate limit, and the ledger balance check. Point the connection string at a separate Neon branch before running them.

## Not done yet 🚧

- No refresh tokens, so you log in again when the token expires
- Funding is simulated, with no real payment provider

## Run it locally ⚙️

You need the .NET 10 SDK, Node, and a PostgreSQL database (Neon works).

1. Set `ConnectionStrings__DefaultConnection`, `Jwt__Key` and `Frontend__Origin` (`http://localhost:5173`) as user secrets or environment variables. Password reset also reads `Email__*` and `App__ResetPasswordUrl`; leave them unset and it just logs instead of sending.
2. Apply migrations with `dotnet ef database update`. On Neon, use the direct host (drop `-pooler`).
3. Start the API: `dotnet run --project ApexPay` (it listens on `:5102` in development).
4. In `ApexPay.Web`, set `VITE_API_BASE_URL=http://localhost:5102`, then `npm install` and `npm run dev`.

## Trade-offs I weighed ⚖️

- **One atomic statement vs locking rows.** Updating a balance in one SQL statement is fast and never waits, but it only fits simple rules.
- **Rules in the database vs only in code.** More work upfront, but a bug can't quietly break the books.
- **404 instead of 403 for someone else's wallet.** It never confirms that an id exists.
- **Token in `sessionStorage`.** It disappears when the tab closes, so you log in again, and there are no refresh tokens yet.
- **Full refunds only.** Fewer edge cases and simpler rules, but less flexible.
- **Free hosting.** It costs nothing, and the API sleeps when idle.

## What this project is really for 🎯

I built ApexPay to sharpen my system design, and to understand trade-offs by living them. Reading about trade-offs or watching a video about them is different from choosing between two options and feeling the cost later.

AI will take over more and more of the routine coding. What stays human is judgment: weighing options, seeing what can go wrong, and explaining why. That's the part I'm training.

---

Built by [ErA-16](https://github.com/ErA-16) · [Source](https://github.com/ErA-16/ApexPay)
