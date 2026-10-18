// Draws the dashboard charts with Chart.js. Loaded on demand as an ES module by the Chart component.
//
// Spec sent from .NET:
//   kind        "columns" (grouped columns), "barsH" (horizontal bars, one series) or "line"
//   labels      category / x labels (already translated)
//   series      [{ name, color, data, pointColors?, extra? }]
//               color / pointColors are names of --viz-* CSS variables (see site.css), so colours follow the theme
//   legend      show the legend (the component sets it for two or more series)
//   locale, currencySymbol, ariaLabel
//
// Colours are read from CSS variables, never hard-coded here, so light and dark mode stay in one place (site.css).

const registry = new Map(); // canvas -> { chart, spec }

const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const token = name => css("--viz-" + name);

function reducedMotion() {
    return window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

// A vertical hairline that follows the pointer on line charts, so readers aim at a day, not at a 2px line.
const crosshair = {
    id: "crosshair",
    afterDatasetsDraw(chart, _args, options) {
        const active = chart.tooltip && chart.tooltip.getActiveElements();
        if (!options.enabled || !active || active.length === 0) { return; }
        const x = active[0].element.x;
        const { top, bottom } = chart.chartArea;
        const ctx = chart.ctx;
        ctx.save();
        ctx.beginPath();
        ctx.moveTo(x, top);
        ctx.lineTo(x, bottom);
        ctx.lineWidth = 1;
        ctx.strokeStyle = options.color;
        ctx.stroke();
        ctx.restore();
    }
};

// Value at the tip of each horizontal bar.
const tipLabels = {
    id: "tipLabels",
    afterDatasetsDraw(chart, _args, options) {
        if (!options.enabled) { return; }
        const meta = chart.getDatasetMeta(0);
        const data = chart.data.datasets[0].data;
        const ctx = chart.ctx;
        ctx.save();
        ctx.font = "600 12px " + options.fontFamily;
        ctx.fillStyle = options.color;
        ctx.textBaseline = "middle";
        meta.data.forEach((bar, i) => {
            ctx.fillText(options.format(data[i]), bar.x + 8, bar.y);
        });
        ctx.restore();
    }
};

function build(canvas, spec) {
    const ink = token("ink");
    const ink2 = token("ink-2");
    const muted = token("muted");
    const grid = token("grid");
    const surface = token("surface");
    const border = token("border");
    const font = getComputedStyle(document.body).fontFamily;

    const number = new Intl.NumberFormat(spec.locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const compact = new Intl.NumberFormat(spec.locale, { notation: "compact", maximumFractionDigits: 1 });
    const money = value => spec.currencySymbol + " " + number.format(value);

    const isBars = spec.kind === "barsH";
    const isLine = spec.kind === "line";

    const datasets = spec.series.map(series => {
        const color = token(series.color);
        const base = { label: series.name, data: series.data, extra: series.extra };

        if (isLine) {
            const last = series.data.length - 1;
            return {
                ...base,
                borderColor: color,
                backgroundColor: color + "1a",       // ~10% wash under the line
                fill: true,
                borderWidth: 2,
                borderJoinStyle: "round",
                borderCapStyle: "round",
                tension: 0,
                pointRadius: ctx => (ctx.dataIndex === last ? 4 : 0),
                pointHoverRadius: 5,
                pointBackgroundColor: color,
                pointBorderColor: surface,           // 2px surface ring keeps the end dot legible
                pointBorderWidth: 2,
                pointHitRadius: 24
            };
        }

        const colors = series.pointColors ? series.pointColors.map(token) : color;
        return {
            ...base,
            backgroundColor: colors,
            hoverBackgroundColor: colors,
            borderWidth: 0,
            hoverBorderWidth: 2,
            hoverBorderColor: ink,
            borderRadius: 4,                         // rounded data end,
            borderSkipped: isBars ? "start" : "bottom", // square at the baseline
            maxBarThickness: isBars ? 18 : 24,
            categoryPercentage: isBars ? 0.8 : 0.7,
            barPercentage: 0.9
        };
    });

    const valueAxis = {
        beginAtZero: true,
        grid: { color: grid, lineWidth: 1, drawTicks: false },
        border: { display: false },
        ticks: { color: muted, padding: 8, maxTicksLimit: 6, callback: value => compact.format(value) }
    };
    const labelAxis = {
        grid: { display: false },
        border: { color: token("axis") },
        ticks: { color: isBars ? ink2 : muted, padding: 8, autoSkip: !isBars, maxRotation: 0 }
    };

    return new Chart(canvas, {
        type: isLine ? "line" : "bar",
        data: { labels: spec.labels, datasets },
        options: {
            indexAxis: isBars ? "y" : "x",
            responsive: true,
            maintainAspectRatio: false,
            animation: reducedMotion() ? false : { duration: 350 },
            layout: { padding: { right: isBars ? 72 : 4, top: 4 } },
            interaction: isLine
                ? { mode: "index", intersect: false }
                : { mode: "nearest", intersect: true },
            scales: isBars ? { x: valueAxis, y: labelAxis } : { x: labelAxis, y: valueAxis },
            plugins: {
                legend: spec.legend
                    ? {
                        position: "top",
                        align: "start",
                        labels: {
                            color: ink2,
                            usePointStyle: true,
                            pointStyle: "rectRounded",
                            boxWidth: 10,
                            boxHeight: 10,
                            padding: 16,
                            font: { family: font }
                        }
                    }
                    : { display: false },
                tooltip: {
                    backgroundColor: surface,
                    borderColor: border,
                    borderWidth: 1,
                    titleColor: ink2,
                    bodyColor: ink,
                    padding: 10,
                    cornerRadius: 8,
                    boxPadding: 4,
                    usePointStyle: true,
                    callbacks: {
                        // Values lead; the series name follows.
                        label: ctx => {
                            const value = isBars ? ctx.parsed.x : ctx.parsed.y;
                            const extra = ctx.dataset.extra ? "  ·  " + ctx.dataset.extra[ctx.dataIndex] : "";
                            return money(value) + "  " + (isBars ? "" : ctx.dataset.label) + extra;
                        },
                        labelPointStyle: () => ({ pointStyle: "line", rotation: 0 })
                    }
                },
                crosshair: { enabled: isLine, color: token("axis") },
                tipLabels: { enabled: isBars, color: ink, fontFamily: font, format: value => money(value) }
            }
        },
        plugins: [crosshair, tipLabels]
    });
}

function draw(canvas, spec) {
    const previous = registry.get(canvas);
    if (previous) { previous.chart.destroy(); }

    canvas.setAttribute("role", "img");
    canvas.setAttribute("aria-label", spec.ariaLabel || "");

    registry.set(canvas, { chart: build(canvas, spec), spec });
}

export function render(canvas, spec) {
    draw(canvas, spec);
}

export function dispose(canvas) {
    const entry = registry.get(canvas);
    if (entry) {
        entry.chart.destroy();
        registry.delete(canvas);
    }
}

// Repaint every chart when the theme changes (the colours are read again from the CSS variables).
document.addEventListener("themechange", () => {
    registry.forEach((entry, canvas) => {
        if (canvas.isConnected) { draw(canvas, entry.spec); } else { registry.delete(canvas); }
    });
});
