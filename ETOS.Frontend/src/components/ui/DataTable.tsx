import type { ReactNode } from "react";

export type DataTableColumn<T> = {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  className?: string;
};

/**
 * Mockup card-row table (border-spacing rows). Server-friendly with render fns.
 * For client sort/filter, use `TanStackDataTable`.
 */
export function DataTable<T>({
  columns,
  rows,
  rowKey,
  emptyMessage = "No records.",
}: {
  columns: DataTableColumn<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  emptyMessage?: string;
}) {
  if (rows.length === 0) {
    return (
      <div className="rounded-etos-card border border-dashed border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        {emptyMessage}
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full border-separate border-spacing-y-2 text-sm">
        <thead>
          <tr className="text-left">
            {columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                className={`px-2.5 pb-1 text-[11px] font-extrabold uppercase tracking-[0.08em] text-etos-ink-muted ${column.className ?? ""}`}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)}>
              {columns.map((column, colIndex) => {
                const isFirst = colIndex === 0;
                const isLast = colIndex === columns.length - 1;
                return (
                  <td
                    key={column.key}
                    className={`border-y border-etos-border-soft bg-etos-panel-muted px-2.5 py-3 text-etos-ink ${
                      isFirst ? "rounded-l-xl border-l font-extrabold" : ""
                    } ${isLast ? "rounded-r-xl border-r" : ""} ${column.className ?? ""}`}
                  >
                    {column.render(row)}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export { TanStackDataTable } from "@/components/ui/TanStackDataTable";
