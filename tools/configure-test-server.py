#!/usr/bin/env python3
"""Prepare a disposable native server configuration; does not start or install services."""
import argparse
from pathlib import Path
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("/srv/seven-days-beyond"))
    parser.add_argument("--user", default="chatgpt")
    args = parser.parse_args()
    base = args.root.resolve()
    # Paths enter systemd directives, so disallow ambiguous whitespace/line breaks.
    if any(c.isspace() for c in str(base)) or not args.user.replace("_", "").replace("-", "").isalnum():
        parser.error("Use a root path without whitespace and a conventional Linux username.")
    template = base / "server/serverconfig.xml"
    tree = ET.parse(template)
    properties = {item.attrib["name"]: item for item in tree.getroot().findall("property")}
    # The shipped template documents this supported setting in a comment.
    if "UserDataFolder" not in properties:
        properties["UserDataFolder"] = ET.SubElement(tree.getroot(), "property", name="UserDataFolder", value="")
    values = {
        "ServerName": "Seven Days Beyond - Baseline Test",
        "ServerDescription": "Disposable baseline before MultiWorld testing",
        "ServerPort": "26900",
        "ServerVisibility": "0",
        "ServerPassword": "",
        "ServerMaxPlayerCount": "6",
        "GameWorld": "Navezgane",
        "GameName": "SDB_Baseline",
        "UserDataFolder": str(base / "data"),
        "EACEnabled": "false",
        "TelnetEnabled": "false",
    }
    missing = set(values) - properties.keys()
    if missing:
        raise SystemExit("The server configuration changed; inspect these missing properties: " + ", ".join(sorted(missing)))
    for name, value in values.items():
        properties[name].set("value", value)
    # Some game builds provide this optional administration interface.
    if "WebDashboardEnabled" in properties:
        properties["WebDashboardEnabled"].set("value", "false")
    for name in ("config", "data", "logs"):
        (base / name).mkdir(parents=True, exist_ok=True)
    config = base / "config/baseline.xml"
    if config.exists():
        raise SystemExit("Refusing to overwrite an existing baseline.xml. Review it before changing the configuration.")
    tree.write(config, encoding="utf-8", xml_declaration=True)
    config.chmod(0o640)
    unit = base / "config/seven-days-beyond.service"
    unit.write_text(f"""[Unit]
Description=Seven Days Beyond disposable baseline dedicated server
After=network-online.target
Wants=network-online.target
StartLimitIntervalSec=300
StartLimitBurst=3

[Service]
Type=simple
User={args.user}
Group={args.user}
WorkingDirectory={base}/server
Environment=HOME=/home/{args.user}
Environment=LD_LIBRARY_PATH={base}/server
ExecStart={base}/server/7DaysToDieServer.x86_64 -logfile {base}/logs/server.log -quit -batchmode -nographics -dedicated -configfile={config}
KillSignal=SIGINT
TimeoutStopSec=180
Restart=on-failure
RestartSec=15
LimitNOFILE=65535
UMask=0027
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
""", encoding="utf-8")
    print("Prepared " + str(config))
    print("Prepared " + str(unit))
    print("No service or firewall changes have been made by this script.")


if __name__ == "__main__":
    main()
