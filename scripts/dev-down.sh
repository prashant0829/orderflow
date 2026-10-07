#!/usr/bin/env bash
pkill -f "OrderFlow\.(Orders|Inventory|Payments|Identity|Gateway)\.(Api|dll)|OrderFlow\.Gateway|OrderFlow\.Notifications" 2>/dev/null
pkill -f "orderflow/web/node_modules" 2>/dev/null
exit 0
