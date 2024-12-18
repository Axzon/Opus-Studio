# Opus Studio


<div align="left">
  <picture>
    <source srcset="https://www.axzon.com/assets/images/logo-blanco-y-rojo-9-545x229.png" height="40px" media="(prefers-color-scheme: dark)">
    <img alt="logo" src="https://www.axzon.com/assets/images/logo-axzon-black.png" height="150px" />
  </picture>
</div>


## Axzon Software Stack 

## Supported Platforms

# Project Structure

Welcome to the Axzon project! This document explains the structure of the repository and how the main Axzon codebase is organized alongside partner-specific contributions.

### Repository Structure

The repository is structured to separate the **main Axzon codebase** from any modifications or contributions made by our partners. Below is an overview of the folder structure:
```bash
root/                        # Main Axzon codebase (DO NOT MODIFY)
│   ├── AxzonOpusStudio/     # Subfolder containing Axzon's core functionality
│   ├── AxzonOpusStudioExe/  # Subfolder containing additional core features
│   └── ...                  # Additional folders/files for the main code
├── partner_name_1/          # Partner-specific folder with modified copies of Axzon code
│   ├── AxzonOpusStudio/     # Copy of AxzonOpusStudio with partner-specific modifications
│   ├── AxzonOpusStudioExe/  # Copy of AxzonOpusStudioExe with partner-specific modifications
│   └── ...                  # Other modified files or folders
├── partner_name_2/          # Another partner-specific folder
│   ├── AxzonOpusStudio/     # Copy of AxzonOpusStudio with their modifications
│   ├── AxzonOpusStudioExe/  # Copy of AxzonOpusStudioExe with their modifications
│   └── ...                  # Other modified files or folders
└── README.md                # This file
```


### Contribution Guidelines

Thank you for your interest in contributing to our project! Please follow the guidelines below to ensure a smooth collaboration and avoid conflicts with the main codebase.

### 1. Clone the repository 
Clone the repository to your local machine using the following command:
  ```bash
    git clone <repository_url>
  ```
   
### 2. Create a folder for your work
In the root directory of the project, create a new folder named after your organization or partner name. 
For example:
  ```bash
    mkdir your_partner_name
  ```
### 3. Copy the base folders into your new folder
Inside your newly created folder, copy the two base folders provided in the repository. These are the folders you are allowed to modify. 
For example:
  ```bash
    cp -r AxzonOpusStudio your_partner_name/
    cp -r AxzonOpusStudioExe your_partner_name/
  ```
### 4. Make your changes
Modify the copied folders within your own folder as needed. Do not modify the original folders located in the root directory.

### 5. Commit your changes
Once you've made your changes, commit them to your branch. Be sure to follow the format below:
  ```bash
    git add .
    git commit -m "Add changes for <your_partner_name>"
  ```
### 6. Push to the repository
Push your changes.

### 7. Submit a pull request
Create a pull request from to the main branch. Add a clear description of your changes and ensure all necessary details are provided for review.

Example Folder Structure
After following the above steps, your folder structure should look like this:

```bash
root/
├── AxzonOpusStudio/           # Original base folder (DO NOT MODIFY)
├── AxzonOpusStudioExe/        # Original base folder (DO NOT MODIFY)
├── your_partner_name/         # Your partner folder
│   ├── AxzonOpusStudio/       # Copy of AxzonOpusStudio (Your modifications here)
│   ├── AxzonOpusStudioExe/    # Copy of AxzonOpusStudioExe (Your modifications here)
```


### Thank you for contributing! If you have any questions, please feel free to reach out.