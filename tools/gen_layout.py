#!/usr/bin/env python3
"""Writes the PMM_C4Timer client files that must agree with the plugin:

  panorama/layout/custom_game/pmm_c4timer.xml         two roots (#c4-root, #c4-prev) with the 8 styles
  panorama/styles/custom_game/pmm_c4timer_ladder.css  class ladders p0..p100, d0..d100, mk5-*, mk10-*

pmm_c4timer.css (colours, sizes, positions) is written by hand.

Usage: python gen_layout.py [panorama folder]
Without an argument the panorama folder is found next to this script:
  ../Content-addonmanager/panorama   (working folder)
  ../PMM_C4Timer/panorama            (GitHub repository)
"""
import os
import sys

SEGMENTS = 20          # style 5; must match .c4-seg width / margin in pmm_c4timer.css
SEG_W = 8              # px
SEG_GAP = 3            # px


def find_panorama():
    here = os.path.dirname(os.path.abspath(__file__))
    for rel in ("../Content-addonmanager/panorama", "../PMM_C4Timer/panorama"):
        p = os.path.normpath(os.path.join(here, rel))
        if os.path.isdir(p):
            return p
    sys.exit("panorama folder not found, pass it as an argument")


def styles_markup(ind):
    segs = "".join('<Panel class="c4-seg" hittest="false" />' for _ in range(SEGMENTS))
    t = ind
    return f"""{t}<!-- 1: text -->
{t}<Panel class="c4-s c4-s1 c4-box c4-blink" hittest="false">
{t}	<Panel class="c4-ic c4-ic-bomb" hittest="false" />
{t}	<Label class="c4-t1 stratum-bold-tf" text="{{s:c4_t}}" />
{t}</Panel>
{t}<!-- 2: bar -->
{t}<Panel class="c4-s c4-s2 c4-box c4-col" hittest="false">
{t}	<Panel class="c4-row" hittest="false">
{t}		<Label class="c4-tag stratum-bold-tf" text="C4 ·" />
{t}		<Label class="c4-tag c4-sitel stratum-bold-tf" text="{{s:c4_site}}" />
{t}		<Panel class="c4-grow" hittest="false" />
{t}		<Label class="c4-n2 stratum-bold-tf" text="{{s:c4_s}}" />
{t}	</Panel>
{t}	<Panel class="c4-track c4-track2" hittest="false"><Panel class="c4-fh c4-pfill" hittest="false" /></Panel>
{t}</Panel>
{t}<!-- 3: ring -->
{t}<Panel class="c4-s c4-s3" hittest="false">
{t}	<Panel class="c4-ring c4-ring-bg" hittest="false" />
{t}	<Panel class="c4-ring c4-ring-fg c4-blink" hittest="false" />
{t}	<Label class="c4-n3 stratum-bold-tf" text="{{s:c4_s}}" />
{t}</Panel>
{t}<!-- 4: card + defuse marks -->
{t}<Panel class="c4-s c4-s4 c4-card c4-col" hittest="false">
{t}	<Panel class="c4-row" hittest="false">
{t}		<Label class="c4-l4 stratum-bold-tf" text="{{s:c4_l_bomb}}" />
{t}		<Label class="c4-l4 c4-sitel stratum-bold-tf" text="{{s:c4_site}}" />
{t}		<Panel class="c4-grow" hittest="false" />
{t}		<Label class="c4-n4 stratum-bold-tf" text="{{s:c4_t}}" />
{t}	</Panel>
{t}	<Panel class="c4-track c4-track4" hittest="false"><Panel class="c4-fh c4-pfill c4-pfill4" hittest="false" /></Panel>
{t}	<Panel class="c4-ticks" hittest="false">
{t}		<Panel class="c4-mk c4-mk10" hittest="false" />
{t}		<Panel class="c4-mk c4-mk5" hittest="false" />
{t}	</Panel>
{t}	<Panel class="c4-row c4-legend" hittest="false">
{t}		<Panel class="c4-ic c4-ic-kit c4-lg5ic" hittest="false" />
{t}		<Label class="c4-lg c4-lg5 stratum-bold-tf" text="{{s:c4_l_kit}}" />
{t}		<Label class="c4-lg c4-lg10 stratum-bold-tf" text="{{s:c4_l_nokit}}" />
{t}	</Panel>
{t}</Panel>
{t}<!-- 5: segments -->
{t}<Panel class="c4-s c4-s5 c4-box c4-col" hittest="false">
{t}	<Panel class="c4-segs" hittest="false">
{t}		<Panel class="c4-seglayer c4-seg-dim" hittest="false">{segs}</Panel>
{t}		<Panel class="c4-seglayer c4-seg-on c4-fseg c4-blink" hittest="false">{segs}</Panel>
{t}	</Panel>
{t}	<Panel class="c4-row c4-center" hittest="false">
{t}		<Label class="c4-tag stratum-bold-tf" text="C4 ·" />
{t}		<Label class="c4-tag c4-n5 stratum-bold-tf" text="{{s:c4_s}}" />
{t}		<Label class="c4-tag stratum-bold-tf" text="{{s:c4_l_sec}}" />
{t}	</Panel>
{t}</Panel>
{t}<!-- 6: vertical bar -->
{t}<Panel class="c4-s c4-s6 c4-box" hittest="false">
{t}	<Panel class="c4-vtrack" hittest="false"><Panel class="c4-fv c4-pfill" hittest="false" /></Panel>
{t}	<Panel class="c4-col c4-v6" hittest="false">
{t}		<Label class="c4-tag stratum-bold-tf" text="C4" />
{t}		<Label class="c4-n6 stratum-bold-tf" text="{{s:c4_s}}" />
{t}		<Label class="c4-tag c4-sitel stratum-bold-tf" text="{{s:c4_site}}" />
{t}	</Panel>
{t}</Panel>
{t}<!-- 8: chip like the round timer -->
{t}<Panel class="c4-s c4-s8 c4-chip" hittest="false">
{t}	<Panel class="c4-chip-ic" hittest="false"><Panel class="c4-ic c4-ic-bomb c4-ic-white" hittest="false" /></Panel>
{t}	<Label class="c4-chip-t c4-blink stratum-bold-tf" text="{{s:c4_m}}" />
{t}</Panel>
{t}<!-- 9: timer + defuse progress -->
{t}<Panel class="c4-s c4-s9 c4-box c4-col" hittest="false">
{t}	<Panel class="c4-row" hittest="false">
{t}		<Label class="c4-tag c4-red stratum-bold-tf" text="C4 ·" />
{t}		<Label class="c4-tag c4-red stratum-bold-tf" text="{{s:c4_site}}" />
{t}		<Panel class="c4-grow" hittest="false" />
{t}		<Label class="c4-n9 stratum-bold-tf" text="{{s:c4_t}}" />
{t}	</Panel>
{t}	<Panel class="c4-track c4-track9" hittest="false"><Panel class="c4-fh c4-pfill" hittest="false" /></Panel>
{t}	<Panel class="c4-def c4-col" hittest="false">
{t}		<Panel class="c4-row" hittest="false">
{t}			<Panel class="c4-ic c4-ic-kit c4-defkit" hittest="false" />
{t}			<Label class="c4-dl stratum-bold-tf" text="{{s:c4_l_def}}" />
{t}			<Panel class="c4-grow" hittest="false" />
{t}			<Label class="c4-dl c4-dp stratum-bold-tf" text="{{s:c4_dp}}" />
{t}		</Panel>
{t}		<Panel class="c4-track c4-dtrack" hittest="false"><Panel class="c4-dfill" hittest="false" /></Panel>
{t}	</Panel>
{t}</Panel>
"""


def layout():
    body = styles_markup("\t\t\t")
    return f"""<root>
	<styles>
		<include src="s2r://panorama/styles/csgostyles.vcss_c" />
		<include src="s2r://panorama/styles/custom_game/pmm_c4timer.vcss_c" />
		<include src="s2r://panorama/styles/custom_game/pmm_c4timer_ladder.vcss_c" />
	</styles>
	<!-- Generated by tools/gen_layout.py (PMM_C4Timer). Edit the script, not this file. -->
	<Panel class="c4-screen" hittest="false">
		<!-- The real timer: state is global (same for everyone), style / position / mute are per player. -->
		<Panel id="c4-root" class="c4-root st-4 pos-top" hittest="false">
{body}		</Panel>
		<!-- !c4 preview: everything on this root is per player. -->
		<Panel id="c4-prev" class="c4-root c4-prev st-4 pos-top" hittest="false">
{body}		</Panel>
	</Panel>
</root>
"""


def pct(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return s + "%"


def ladder():
    out = ["/* Generated by tools/gen_layout.py (PMM_C4Timer). Edit the script, not this file.",
           "   pK  = time left, K percent of the timer (bars, ring, segments)",
           "   dK  = defuse progress, K percent",
           "   mk5-K / mk10-K = where 5 s / 10 s of time left sit on the bar of style 4 */", ""]
    total = SEGMENTS * SEG_W + (SEGMENTS - 1) * SEG_GAP
    for k in range(101):
        n = -(-k * SEGMENTS // 100)   # ceil
        seg_right = 0.0 if n == 0 else (n * (SEG_W + SEG_GAP) - SEG_GAP) / total * 100
        out.append(f".p{k} .c4-fh {{ clip: rect( 0%, {pct(k)}, 100%, 0% ); }}")
        out.append(f".p{k} .c4-fv {{ clip: rect( {pct(100 - k)}, 100%, 100%, 0% ); }}")
        out.append(f".p{k} .c4-fseg {{ clip: rect( 0%, {pct(seg_right)}, 100%, 0% ); }}")
        out.append(f".p{k} .c4-ring-fg {{ clip: radial( 50% 50%, 0deg, {k * 3.6:.1f}deg ); }}")
    out.append("")
    for k in range(101):
        out.append(f".d{k} .c4-dfill {{ clip: rect( 0%, {pct(k)}, 100%, 0% ); }}")
    out.append("")
    for name in ("mk5", "mk10"):
        for k in range(101):
            # A 2 px tick moved by its margin; no full-width clipped panels over the bar.
            out.append(f".{name}-{k} .c4-{name} {{ margin-left: {pct(min(k, 99))}; }}")
        out.append("")
    return "\n".join(out)


def main():
    pano = sys.argv[1] if len(sys.argv) > 1 else find_panorama()
    lay = os.path.join(pano, "layout", "custom_game")
    sty = os.path.join(pano, "styles", "custom_game")
    os.makedirs(lay, exist_ok=True)
    os.makedirs(sty, exist_ok=True)
    with open(os.path.join(lay, "pmm_c4timer.xml"), "w", encoding="utf-8", newline="\n") as f:
        f.write(layout())
    with open(os.path.join(sty, "pmm_c4timer_ladder.css"), "w", encoding="utf-8", newline="\n") as f:
        f.write(ladder())
    print("written:", os.path.join(lay, "pmm_c4timer.xml"))
    print("written:", os.path.join(sty, "pmm_c4timer_ladder.css"))


if __name__ == "__main__":
    main()
