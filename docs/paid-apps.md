# Updating paid apps and choosing price tiers

[Back to the README](../README.md)

This guide applies to packaged (MSIX) app submissions, not MSI/EXE submissions or package flights. Before publishing, [install and configure the CLI](https://aka.ms/msstoredevcli/docs) and ensure the app already exists in Partner Center.

## Publish an update without changing the price

Replace `YOUR_APP_ID` and the package path in these PowerShell examples with your own values:

```powershell
msstore publish .\MyApp.msix --appId YOUR_APP_ID
```

Paid apps are supported. When the Store returns a usable existing `Pricing.PriceId`, the CLI sends it back unchanged. You do not need `--priceId` for every paid-app update.

If the Store returns `Base`, an empty price ID, or no pricing object, the CLI stops rather than submitting unsafe pricing. This can happen for apps whose prices are managed per market in Partner Center. Do not remove pricing to bypass this check: submission updates replace the entire submission, rather than patching individual properties.

| Pricing in an update | Consequence |
| --- | --- |
| Base `PriceId` is `Base` | The submission API rejects it as a base price. |
| `PriceId` is empty, null, or omitted from a pricing object | The API can accept it and silently reset the product to free; the CLI blocks this. |
| The entire pricing object is missing or null | The submission API rejects the update; the CLI blocks this. |

For per-market pricing, publishing through Partner Center is the safer alternative. Otherwise, explicitly choose the base tier you intend to set.

## Set an explicit base price and review the draft

```powershell
msstore publish .\MyApp.msix --appId YOUR_APP_ID --priceId Tier1012 --noCommit
```

`--priceId` (alias `-pid`) overrides the base price, even if the app already has a valid tier. `Tier1012` is illustrative: verify the tier's current price for your account before using it. `--noCommit` (alias `-nc`) uploads the package and updates the draft but does not commit it. It is not a dry run.

> [!WARNING]
> Setting a base tier does not guarantee that newer per-market pricing is preserved. Confirm all intended market prices before committing; do not treat the override as a way to automatically recover the app's current prices.

Retrieve the submission to inspect its complete pricing object:

```powershell
msstore submission get YOUR_APP_ID --output-stream stderr
```

For packaged apps, this returns the pending submission if one exists, otherwise the last published submission. Inspect `Pricing.PriceId` and `Pricing.MarketSpecificPricings`; this command does not resolve tier IDs to currency amounts. The explicit output-stream option keeps JSON clean even if the environment routes human-readable output to stdout.

After reviewing the draft and verifying the intended prices, commit the existing draft:

```powershell
msstore submission publish YOUR_APP_ID
```

> [!IMPORTANT]
> Keep submission editing in one workflow. [Microsoft's submission API guidance](https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-app-submissions) warns that editing an API-created submission in Partner Center prevents further API changes or commits and can leave the submission in an error state. Reviewing is not the same as editing; do not edit the draft in Partner Center and then attempt to commit it with the CLI.

## Price IDs

| Value | Meaning |
| --- | --- |
| `Tier...` (for example, `Tier1012`) | A base-price tier, not a literal currency amount. Confirm its market-specific price in Partner Center. |
| `Free` | Makes the app free. Do not use it as a workaround for a paid app. |
| `NotAvailable` | Indicates unavailability in the applicable market; it is not a paid tier. |
| `Base` | A sentinel for using the app's base price in market-specific pricing. It cannot be used as the base `Pricing.PriceId` or supplied to `--priceId`. |

The [official price-tier reference](https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-app-submissions#price-tiers) documents legacy tiers `Tier2` through `Tier96` and expanded tiers `Tier1012` through `Tier1424`. These are documented catalog ranges, not a guarantee that any particular tier is accepted for your product. The CLI validates the option's format and leaves tier availability to the service; it does not use `IsAdvancedPricingModel` to select or enforce a range.

### USD tier mapping table

The expanded pricing model uses the following US-market retail prices. Tier ranges are inclusive, and the increment applies **within each row**, not across the whole catalog.

| Tier range (inclusive) | Retail price range (USD) | Increase per tier |
| --- | --- | --- |
| `Tier1012` - `Tier1102` | $0.99 - $9.99 | $0.10 |
| `Tier1103` - `Tier1282` | $10.49 - $99.99 | $0.50 |
| `Tier1283` - `Tier1382` | $100.99 - $199.99 | $1.00 |
| `Tier1383` - `Tier1402` | $209.99 - $399.99 | $10.00 |
| `Tier1403` - `Tier1414` | $449.99 - $999.99 | $50.00 |
| `Tier1415` - `Tier1424` | $1,099.99 - $1,999.99 | $100.00 |

For example, `Tier1012` corresponds to $0.99, `Tier1052` to $4.99, and `Tier1102` to $9.99 in this reference. To find a tier within a row, start at its first tier and advance one tier for each listed price increment. Do not extrapolate across row boundaries or use these mappings for legacy tiers.

> [!IMPORTANT]
> This table is a USD reference, not a guarantee of current prices for your account or other markets. Confirm the intended price and any market-specific overrides in Partner Center before committing.

### Confirm prices in Partner Center

For the authoritative table available to your account, open an app submission in **Partner Center > Pricing and availability**, then choose **view table** under **Markets and custom prices** (or **Pricing**, depending on the account). Locate the **United States / USD** prices and match the desired amount to its tier ID.

This location is documented in the [Microsoft Learn price-tier reference](https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-app-submissions#price-tiers). Use it for current account-specific mappings and other currencies. The CLI does not provide a tier-to-currency lookup.

Use the current Partner Center table to confirm the price before committing. For the pricing object's fields and market overrides, see the [pricing resource reference](https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-app-submissions#pricing-resource).

## Updating submission JSON directly

For advanced workflows, `submission update` takes a **complete submission payload**, not a partial pricing patch:

```powershell
msstore submission update YOUR_APP_ID --payload .\submission.json
```

Start from the full submission returned by `submission get`, preserve the other fields, and edit the intended values in `submission.json`. Keep `Pricing.PriceId` set to the desired valid tier (or intentionally to `Free` or `NotAvailable`). Do not send a JSON object containing only pricing.

This command updates the draft without committing it. Review it before using `submission publish`. The same base-price and per-market warnings apply; supplying a valid ID does not verify that it represents your intended price.
