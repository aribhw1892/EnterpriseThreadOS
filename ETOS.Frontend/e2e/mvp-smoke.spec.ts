import { expect, test } from "@playwright/test";

test.describe("MVP demo smoke", () => {
  test("home shows health and MVP checklist", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("heading", { name: /EnterpriseThreadOS/i })).toBeVisible();
    await expect(page.getByRole("heading", { name: "MVP demonstration checklist" })).toBeVisible();
    await expect(page.getByText(/Backend API base URL/i)).toBeVisible();
  });

  test("imports page shows demo harness buttons", async ({ page }) => {
    await page.goto("/imports");
    await expect(page.getByRole("heading", { name: /Import Mapping and Staging/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /Run identity demo/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /Run BOM comparison/i })).toBeVisible();
  });

  test("chat page renders session UI shell", async ({ page }) => {
    await page.goto("/chat");
    await expect(page.getByRole("heading", { name: /Governed Chat/i })).toBeVisible();
  });

  test("workflows page lists bom-impact-review affordances", async ({ page }) => {
    await page.goto("/workflows");
    await expect(page.getByRole("heading", { name: /Tenant Workflows/i })).toBeVisible();
    await expect(page.getByText(/bom-impact-review/i)).toBeVisible();
  });
});
