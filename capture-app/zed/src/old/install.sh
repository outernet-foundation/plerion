#!/usr/bin/env bash
set -euo pipefail

SERVICE_NAME=zed-capture.service
SYSTEMD_DIR=/etc/systemd/system

# 1) Copy your unit file
echo "Installing $SERVICE_NAME → $SYSTEMD_DIR/"
sudo cp "$SERVICE_NAME" "$SYSTEMD_DIR/"
sudo chmod 644 "$SYSTEMD_DIR/$SERVICE_NAME"

# 2) Reload systemd and enable the service
echo "Reloading systemd daemon…"
sudo systemctl daemon-reload

echo "Enabling $SERVICE_NAME…"
sudo systemctl enable "$SERVICE_NAME"

echo "Restarting $SERVICE_NAME to pick up changes…"
sudo systemctl daemon-reload
sudo systemctl restart "$SERVICE_NAME"

# 3) Ensure the USB-gadget mode is on
echo "Enabling USB device-mode service…"
sudo systemctl enable --now nv-l4t-usb-device-mode.service

# 4) Ensure dnsmasq is installed
if ! dpkg -s dnsmasq >/dev/null 2>&1; then
  echo "dnsmasq not found—installing..."
  sudo apt-get update
  sudo apt-get install -y dnsmasq
fi

# 5) Configure dnsmasq for USB gadget DHCP
echo "Writing /etc/dnsmasq.d/usb-gadget.conf…"
cat << 'EOF' | sudo tee /etc/dnsmasq.d/usb-gadget.conf >/dev/null
interface=usb0
dhcp-range=192.168.55.100,192.168.55.200,255.255.255.0,12h
EOF

# 6) Restart dnsmasq (try systemd first, then fallback)
echo "Restarting dnsmasq…"
if sudo systemctl restart dnsmasq.service 2>/dev/null; then
  echo "dnsmasq restarted via systemctl"
elif sudo service dnsmasq restart 2>/dev/null; then
  echo "dnsmasq restarted via service"
else
  echo "Warning: could not restart dnsmasq automatically"
fi

echo "✔ All done! REST service, USB gadget + DHCP are enabled."
