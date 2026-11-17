function updateDateTime() {

    const element = document.getElementById("currentDateTime");

    if (!element)
        return;

    const now = new Date();

    const year = now.getFullYear();

    const month = String(now.getMonth() + 1).padStart(2, "0");

    const day = String(now.getDate()).padStart(2, "0");

    const hours = String(now.getHours()).padStart(2, "0");

    const minutes = String(now.getMinutes()).padStart(2, "0");

    const seconds = String(now.getSeconds()).padStart(2, "0");

    element.textContent =
        `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
}


updateDateTime();

setInterval(updateDateTime, 1000);


document.addEventListener("DOMContentLoaded", function () {

    const dropdown = document.getElementById("cpkDropdown");

    if (!dropdown) {
        return;
    }

    const button = document.getElementById("cpkDropdownButton");
    const menu = document.getElementById("cpkDropdownMenu");
    const hiddenInput = document.getElementById("cpkLevel");
    const selectedText = document.getElementById("cpkSelectedText");

    button.addEventListener("click", function (event) {

        event.stopPropagation();

        dropdown.classList.toggle("open");

    });

    menu.querySelectorAll(".cpk-option").forEach(function (option) {

        option.addEventListener("click", function () {

            const value = this.getAttribute("data-value");
            const text = this.textContent.trim();

            hiddenInput.value = value;
            selectedText.textContent = text;

            menu.querySelectorAll(".cpk-option").forEach(function (item) {
                item.classList.remove("selected");
            });

            this.classList.add("selected");

            dropdown.classList.remove("open");

        });

    });

    document.addEventListener("click", function (event) {

        if (!dropdown.contains(event.target)) {
            dropdown.classList.remove("open");
        }

    });

});