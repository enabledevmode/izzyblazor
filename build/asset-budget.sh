#!/usr/bin/env bash
# Gzip size budgets for the library's CSS and JS. CI and Release both run this.
set -eo pipefail
shopt -s nullglob
css=$(gzip -c src/IzzyBlazor.Core/wwwroot/izzyblazor.css | wc -c)
js=$(gzip -c src/IzzyBlazor.Core/wwwroot/IzzyBlazor.Core.lib.module.js | wc -c)
date=$(gzip -c src/IzzyBlazor.Core/wwwroot/date.css | wc -c)
carousel=$(gzip -c src/IzzyBlazor.Core/wwwroot/carousel.css | wc -c)
chart=$(gzip -c src/IzzyBlazor.Core/wwwroot/chart.css | wc -c)
heatmap=$(gzip -c src/IzzyBlazor.Core/wwwroot/heatmap.css | wc -c)
radar=$(gzip -c src/IzzyBlazor.Core/wwwroot/radar.css | wc -c)
sankey=$(gzip -c src/IzzyBlazor.Core/wwwroot/sankey.css | wc -c)
treemap=$(gzip -c src/IzzyBlazor.Core/wwwroot/treemap.css | wc -c)
bullet=$(gzip -c src/IzzyBlazor.Core/wwwroot/bullet.css | wc -c)
gauge=$(gzip -c src/IzzyBlazor.Core/wwwroot/gauge.css | wc -c)
tabsedit=$(gzip -c src/IzzyBlazor.Core/wwwroot/tabs-edit.css | wc -c)
speeddial=$(gzip -c src/IzzyBlazor.Core/wwwroot/speed-dial.css | wc -c)
treeselect=$(gzip -c src/IzzyBlazor.Core/wwwroot/treeselect.css | wc -c)
bands=$(gzip -c src/IzzyBlazor.Core/wwwroot/bands.css | wc -c)
picker=$(gzip -c src/IzzyBlazor.Core/wwwroot/column-picker.css | wc -c)
richtext=$(gzip -c src/IzzyBlazor.Core/wwwroot/rich-text.css | wc -c)
richcontent=$(gzip -c src/IzzyBlazor.Core/wwwroot/rich-content.css | wc -c)
colorpanel=$(gzip -c src/IzzyBlazor.Core/wwwroot/color-panel.css | wc -c)
chat=$(gzip -c src/IzzyBlazor.Core/wwwroot/chat.css | wc -c)
quote=$(gzip -c src/IzzyBlazor.Core/wwwroot/blockquote.css | wc -c)
comment=$(gzip -c src/IzzyBlazor.Core/wwwroot/comment.css | wc -c)
mentions=$(gzip -c src/IzzyBlazor.Core/wwwroot/mentions.css | wc -c)
sheet=$(gzip -c src/IzzyBlazor.Core/wwwroot/action-sheet.css | wc -c)
rangesel=$(gzip -c src/IzzyBlazor.Core/wwwroot/range-selector.css | wc -c)
sched=$(gzip -c src/IzzyBlazor.Core/wwwroot/scheduler.css | wc -c)
echo "CSS gzip: $css bytes (budget 52224)"
echo "date.css gzip: $date bytes (budget 3072; only pages with a calendar load it)"
echo "carousel.css gzip: $carousel bytes (budget 2048; only pages with a carousel load it)"
echo "chart.css gzip: $chart bytes (budget 6400; only pages with a chart load it)"
echo "heatmap.css gzip: $heatmap bytes (budget 2048; only pages with a HeatmapChart or CalendarHeatmap load it)"
echo "radar.css gzip: $radar bytes (budget 1024; only pages with a RadarChart load it)"
echo "sankey.css gzip: $sankey bytes (budget 1024; only pages with a SankeyChart load it)"
echo "treemap.css gzip: $treemap bytes (budget 1024; only pages with a TreemapChart load it)"
echo "bullet.css gzip: $bullet bytes (budget 1536; only pages with a BulletChart load it)"
echo "gauge.css gzip: $gauge bytes (budget 2048; only pages with a LinearGauge, or a Gauge with pointers or a band name, load it)"
echo "tabs-edit.css gzip: $tabsedit bytes (budget 1024; only tabs with close or add controls load it)"
echo "speed-dial.css gzip: $speeddial bytes (budget 1024; only pages with a SpeedDial load it)"
echo "treeselect.css gzip: $treeselect bytes (budget 1024; only pages with a TreeSelect load it)"
echo "bands.css gzip: $bands bytes (budget 1024; only tables with column bands load it)"
echo "column-picker.css gzip: $picker bytes (budget 1024; only grids with a column picker load it)"
echo "rich-text.css gzip: $richtext bytes (budget 2048; only pages with a RichTextEditor load it)"
echo "rich-content.css gzip: $richcontent bytes (budget 1024; only pages with rich text that uses its classes load it)"
echo "color-panel.css gzip: $colorpanel bytes (budget 2048; only pages with a ColorPanel load it)"
echo "chat.css gzip: $chat bytes (budget 2048; only pages with a Chat load it)"
echo "blockquote.css gzip: $quote bytes (budget 1024; only pages with a Blockquote load it)"
echo "comment.css gzip: $comment bytes (budget 1024; only pages with a Comment load it)"
echo "mentions.css gzip: $mentions bytes (budget 1024; only pages with Mentions or MentionText load it)"
echo "action-sheet.css gzip: $sheet bytes (budget 1024; only pages with an ActionSheet load it)"
echo "range-selector.css gzip: $rangesel bytes (budget 2048; only charts with a RangeSelector load it)"
echo "scheduler.css gzip: $sched bytes (budget 3072; only pages with a Scheduler load it)"
echo "JS gzip:  $js bytes (budget 15424; every page loads it)"
# Elements most pages don't have load on demand, one file each (Scripts/lazy.ts).
# The built files must be exactly the ones lazy.ts loads.
expected=$(grep -A1 'const lazyNames =' src/IzzyBlazor.Core/Scripts/lazy.ts | grep -o '"[a-z -]*"' | head -1 | tr -d '"' | tr ' ' '\n' | sed 's|$|.js|' | sort)
files=(src/IzzyBlazor.Core/wwwroot/elements/*.js)
built=$(for f in "${files[@]}"; do basename "$f"; done | sort)
echo "On-demand elements: ${#files[@]} files"
test -n "$expected" && test "$built" = "$expected"
big=0
richcore=0
for f in "${files[@]}"; do
  n=$(gzip -c "$f" | wc -c)
  echo "  $(basename "$f"): $n bytes"
  # The rich text editor's core has a budget of its own (its features are parts).
  if [ "$(basename "$f")" = "rich-text.js" ]; then richcore=$n; continue; fi
  if [ "$n" -gt "$big" ]; then big=$n; fi
done
# Parts an element loads only when needed (elements/parts), on the same budget.
for f in src/IzzyBlazor.Core/wwwroot/elements/parts/*.js; do
  n=$(gzip -c "$f" | wc -c)
  echo "  parts/$(basename "$f"): $n bytes"
  if [ "$n" -gt "$big" ]; then big=$n; fi
done
echo "Largest on-demand element: $big bytes (budget 6656)"
echo "rich-text.js: $richcore bytes (budget 8192; its tables, images, links, formats and Markdown are parts)"
test "$css" -le 52224 && test "$date" -le 3072 && test "$carousel" -le 2048 && test "$chart" -le 6400 && test "$heatmap" -le 2048 && test "$radar" -le 1024 && test "$sankey" -le 1024 && test "$treemap" -le 1024 && test "$bullet" -le 1536 && test "$gauge" -le 2048 && test "$tabsedit" -le 1024 && test "$speeddial" -le 1024 && test "$treeselect" -le 1024 && test "$bands" -le 1024 && test "$picker" -le 1024 && test "$richtext" -le 2048 && test "$richcontent" -le 1024 && test "$colorpanel" -le 2048 && test "$chat" -le 2048 && test "$quote" -le 1024 && test "$comment" -le 1024 && test "$mentions" -le 1024 && test "$sheet" -le 1024 && test "$rangesel" -le 2048 && test "$sched" -le 3072 && test "$js" -le 15424 && test "$big" -le 6656 && test "$richcore" -le 8192
