window.setupClickOutside = (dotNetRef) => {
    const handleClick = (event) => {
        const dropdowns = document.querySelectorAll('.toolbox-dropdown');
        dropdowns.forEach(dropdown => {
            if (dropdown && !dropdown.contains(event.target)) {
                const dropdownMenu = dropdown.querySelector('.toolbox-dropdown-menu');
                if (dropdownMenu) {
                    dotNetRef.invokeMethodAsync('CloseDropdownFromJs');
                }
            }
        });
    };

    // Remove any existing listener first
    document.removeEventListener('click', handleClick);
    // Add the new listener
    document.addEventListener('click', handleClick);
};