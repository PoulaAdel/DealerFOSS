// DealProducts — the F&I menu on a deal: warranties, GAP, service plans.
//
// Use:  mounted by DealsPage while the deal's terms are open.
// Edit: three things here are deliberate.
//
//       (1) The price and cost boxes are seeded from the catalogue and then
//       EDITABLE. F&I is negotiated — the same warranty goes out at different
//       prices on different deals — and the figure typed here is the one that
//       gets recorded. A read-only price would make the whole capability wrong.
//
//       (2) Cost and gross are shown to staff and belong on no customer-facing
//       output. When a printed deal summary arrives, it takes `price` only.
//
//       (3) Saving replaces the whole set, like the charges editor, so this is
//       seeded from what the deal already sold. An empty form would mean ticking
//       one product deleted the rest.

import { useEffect, useState } from 'react';
import { api, post } from '../../shared/api';
import type { DealDetail, FinanceProductView } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

/** What the salesperson is choosing between, with this deal's figures on it. */
type Row = {
  product: FinanceProductView;
  selected: boolean;
  price: string;
  cost: string;
};

export function DealProducts({
  deal,
  onChanged,
}: {
  deal: DealDetail;
  onChanged: (updated: DealDetail) => void;
}) {
  const { t, format } = useI18n();
  const describe = useApiMessage();
  const money = (amount: number) => format.money(amount, deal.currency);

  const [rows, setRows] = useState<Row[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let current = true;

    void (async () => {
      try {
        // Withdrawn products are excluded from the menu, but one already sold on
        // this deal has to stay visible — otherwise saving would silently drop it.
        const available = await api<FinanceProductView[]>('/finance/products?availableOnly=true');
        const sold = deal.products;

        const menu = [...available];
        for (const line of sold) {
          if (!menu.some((p) => p.id === line.financeProductId)) {
            menu.push({
              id: line.financeProductId,
              name: line.name,
              kind: 'Other',
              provider: line.provider ?? t('products.withdrawn'),
              defaultPrice: line.price,
              defaultCost: line.cost,
              currency: deal.currency,
              termMonths: line.termMonths,
              termMiles: line.termMiles,
              isAvailable: false,
            });
          }
        }

        if (current) {
          setRows(
            menu.map((product) => {
              const line = sold.find((s) => s.financeProductId === product.id);

              return {
                product,
                selected: line !== undefined,
                // Seeded from what was agreed if it is already on the deal, and
                // from the catalogue otherwise.
                price: String(line?.price ?? product.defaultPrice),
                cost: String(line?.cost ?? product.defaultCost),
              };
            }),
          );
        }
      } catch {
        if (current) {
          setRows([]);
        }
      }
    })();

    return () => {
      current = false;
    };
    // Deliberately keyed on the deal, so reopening a different deal reseeds.
  }, [deal]);

  if (rows === null) {
    return <p className="note">{t('products.loading')}</p>;
  }

  if (rows.length === 0) {
    return <p className="note">{t('products.none')}</p>;
  }

  const chosen = rows.filter((r) => r.selected);
  const total = chosen.reduce((sum, r) => sum + (Number(r.price) || 0), 0);
  const gross = chosen.reduce((sum, r) => sum + (Number(r.price) || 0) - (Number(r.cost) || 0), 0);

  function update(id: string, change: Partial<Row>) {
    setRows((current) =>
      (current ?? []).map((r) => (r.product.id === id ? { ...r, ...change } : r)),
    );
  }

  async function save() {
    setBusy(true);
    setError(null);

    try {
      onChanged(
        await post<DealDetail>(`/deals/${deal.id}/products`, {
          products: chosen.map((r) => ({
            financeProductId: r.product.id,
            price: Number(r.price) || 0,
            cost: Number(r.cost) || 0,
            termMonths: r.product.termMonths,
            termMiles: r.product.termMiles,
          })),
        }),
      );
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h3>{t('products.title')}</h3>
      <p className="note">{t('products.lede')}</p>

      <div className="scroll">
        <table className="table terms">
          <thead>
            <tr>
              <th scope="col">{t('products.colSell')}</th>
              <th scope="col">{t('products.colProduct')}</th>
              <th scope="col" className="num">
                {t('products.colPrice')}
              </th>
              <th scope="col" className="num">
                {t('products.colCost')}
              </th>
              <th scope="col" className="num">
                {t('products.colGross')}
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.product.id} className={row.selected ? undefined : 'muted'}>
                <td>
                  <input
                    type="checkbox"
                    aria-label={t('products.sellThis', { product: row.product.name })}
                    checked={row.selected}
                    disabled={busy}
                    onChange={(event) => update(row.product.id, { selected: event.target.checked })}
                  />
                </td>
                <td>
                  {/* The product NAME and PROVIDER are catalogue records the
                      dealership typed, so they are printed as stored. Only the
                      term and the withdrawn marker are our words. */}
                  {row.product.name}
                  <div className="muted">
                    {row.product.provider}
                    {row.product.termMonths === null
                      ? ''
                      : ` · ${t('products.termMonths', { count: row.product.termMonths })}`}
                    {row.product.isAvailable ? '' : ` · ${t('products.withdrawn')}`}
                  </div>
                </td>
                <td className="num">
                  <input
                    inputMode="decimal"
                    aria-label={t('products.priceFor', { product: row.product.name })}
                    value={row.price}
                    disabled={busy || !row.selected}
                    onChange={(event) => update(row.product.id, { price: event.target.value })}
                  />
                </td>
                <td className="num">
                  <input
                    inputMode="decimal"
                    aria-label={t('products.costOf', { product: row.product.name })}
                    value={row.cost}
                    disabled={busy || !row.selected}
                    onChange={(event) => update(row.product.id, { cost: event.target.value })}
                  />
                </td>
                <td className="num">
                  {row.selected
                    ? money((Number(row.price) || 0) - (Number(row.cost) || 0))
                    : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="note">
        {chosen.length === 0
          ? t('products.nothingSelected')
          : t('products.addedToDeal', { added: money(total), gross: money(gross) })}
      </p>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button type="button" disabled={busy} onClick={() => void save()}>
          {busy ? t('common.saving') : t('products.saveWhatIsSold')}
        </button>
      </div>
    </>
  );
}
