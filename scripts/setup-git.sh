#!/bin/bash

echo "Setting up Git identity..."
read -p "Enter your name: " GNAME
read -p "Enter your email: " GMAIL

git config --global user.name "$GNAME"
git config --global user.email "$GMAIL"

if ! command -v gh &> /dev/null
then
    echo "GitHub CLI (gh) not found. Please install it from https://cli.github.com/"
    exit 1
fi

echo ""
echo "Starting GitHub authentication..."
gh auth login

echo ""
echo "Git identity and GitHub authentication setup complete."
git config --list --global | grep "user"
gh auth status