using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    /// <summary>
    /// The "rule checker" for suppliers. Every Add / Update / Deactivate /
    /// Reactivate request passes through here FIRST. It:
    ///   1. Makes sure only the Owner is doing this (Cashier is blocked, even at data level).
    ///   2. Cleans and validates the input (required fields, lengths, phone format, duplicates).
    ///   3. Calls SupplierRepository to do the actual database work.
    ///   4. Catches any database error and returns a friendly message instead of crashing.
    /// Every method answers with (Success, ErrorMessage) so the form never needs try/catch.
    /// </summary>
    public class SupplierService
    {
        private readonly SupplierRepository _repository;

        // These match the real column sizes in the Suppliers table (checked in SSMS).
        private const int SupplierNameMaxLength = 150;
        private const int AddressMaxLength = 250;

        // PH mobile number rule: starts with "09", exactly 11 digits, digits only.
        // Valid: 09171234567   Invalid: 0917-123-4567, +639171234567, 9171234567
        private static readonly Regex ContactNumberPattern = new(@"^09\d{9}$", RegexOptions.Compiled);

        private const string DbErrorMessage = "Cannot connect to the database. Please try again or contact support.";
        private const string AccessDeniedMessage = "Access denied. Only the Owner can manage suppliers.";
        private const string NotFoundMessage = "The selected supplier no longer exists. Please refresh the list.";

        public SupplierService()
        {
            _repository = new SupplierRepository();
        }

        // ==================== READ ====================

        /// <summary>Gets every supplier (active + inactive) — used to fill the grid.</summary>
        public async Task<(bool Success, string ErrorMessage, List<Supplier> Suppliers)> GetAllAsync()
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage, new List<Supplier>());

            try
            {
                var suppliers = await _repository.GetAllAsync();
                return (true, string.Empty, suppliers.ToList());
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, new List<Supplier>());
            }
        }

        /// <summary>Gets only ACTIVE suppliers — will be used by the Stock-In dropdown in Module 5.</summary>
        public async Task<(bool Success, string ErrorMessage, List<Supplier> Suppliers)> GetActiveAsync()
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage, new List<Supplier>());

            try
            {
                var suppliers = await _repository.GetActiveAsync();
                return (true, string.Empty, suppliers.ToList());
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, new List<Supplier>());
            }
        }

        // ==================== ADD ====================

        public async Task<(bool Success, string ErrorMessage)> AddAsync(string? supplierName, string? contactNumber, string? address)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage);

            var (name, contact, cleanAddress) = CleanInput(supplierName, contactNumber, address);

            var (isValid, validationError) = ValidateInput(name, contact, cleanAddress);
            if (!isValid)
                return (false, validationError);

            try
            {
                // Duplicate check (case-insensitive) — no other supplier may have this name.
                var existing = await _repository.GetByNameAsync(name);
                if (existing != null)
                    return (false, $"A supplier named \"{existing.SupplierName}\" already exists.");

                var supplier = new Supplier
                {
                    SupplierName = name,
                    ContactNumber = contact,
                    Address = cleanAddress
                };

                await _repository.InsertAsync(supplier);
                return (true, string.Empty);
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                // Safety net: the UNIQUE constraint on SupplierName in the database
                // rejected a duplicate that slipped past the check above.
                return (false, "A supplier with this name already exists.");
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        // ==================== UPDATE ====================

        public async Task<(bool Success, string ErrorMessage)> UpdateAsync(int supplierId, string? supplierName, string? contactNumber, string? address)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage);

            if (supplierId <= 0)
                return (false, "Please select a supplier to update.");

            var (name, contact, cleanAddress) = CleanInput(supplierName, contactNumber, address);

            var (isValid, validationError) = ValidateInput(name, contact, cleanAddress);
            if (!isValid)
                return (false, validationError);

            try
            {
                var current = await _repository.GetByIdAsync(supplierId);
                if (current == null)
                    return (false, NotFoundMessage);

                // Duplicate check — but ignore the supplier's OWN row, so keeping
                // the same name (or only changing its letter casing) is allowed.
                var existing = await _repository.GetByNameAsync(name);
                if (existing != null && existing.SupplierId != supplierId)
                    return (false, $"Another supplier named \"{existing.SupplierName}\" already exists.");

                current.SupplierName = name;
                current.ContactNumber = contact;
                current.Address = cleanAddress;

                await _repository.UpdateAsync(current);
                return (true, string.Empty);
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return (false, "Another supplier with this name already exists.");
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        // ==================== DEACTIVATE / REACTIVATE ====================

        public async Task<(bool Success, string ErrorMessage)> DeactivateAsync(int supplierId)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage);

            if (supplierId <= 0)
                return (false, "Please select a supplier to deactivate.");

            try
            {
                var current = await _repository.GetByIdAsync(supplierId);
                if (current == null)
                    return (false, NotFoundMessage);

                if (!current.IsActive)
                    return (false, "This supplier is already inactive.");

                // NOTE: No Stock-In history check here — deactivating is ALWAYS allowed.
                // Past Stock-Ins keep their own snapshot of cost/quantity, so history stays accurate.
                // Deactivating only stops this supplier from being picked for FUTURE Stock-Ins.
                await _repository.SetActiveStatusAsync(supplierId, false);
                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        public async Task<(bool Success, string ErrorMessage)> ReactivateAsync(int supplierId)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage);

            if (supplierId <= 0)
                return (false, "Please select a supplier to reactivate.");

            try
            {
                var current = await _repository.GetByIdAsync(supplierId);
                if (current == null)
                    return (false, NotFoundMessage);

                if (current.IsActive)
                    return (false, "This supplier is already active.");

                await _repository.SetActiveStatusAsync(supplierId, true);
                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        // ==================== PRIVATE HELPERS ====================

        // Trims spaces off both ends. An empty address becomes null (stored as NULL in the DB).
        private static (string Name, string Contact, string? Address) CleanInput(string? supplierName, string? contactNumber, string? address)
        {
            string name = (supplierName ?? string.Empty).Trim();
            string contact = (contactNumber ?? string.Empty).Trim();
            string? cleanAddress = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
            return (name, contact, cleanAddress);
        }

        // Checks the cleaned values against the business rules. First failure wins.
        private static (bool IsValid, string ErrorMessage) ValidateInput(string name, string contact, string? address)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (false, "Supplier name is required.");

            if (name.Length > SupplierNameMaxLength)
                return (false, $"Supplier name cannot be longer than {SupplierNameMaxLength} characters.");

            if (string.IsNullOrWhiteSpace(contact))
                return (false, "Contact number is required.");

            if (!ContactNumberPattern.IsMatch(contact))
                return (false, "Contact number must be a valid PH mobile number: 11 digits starting with 09 (e.g., 09171234567). No spaces or dashes.");

            if (address != null && address.Length > AddressMaxLength)
                return (false, $"Address cannot be longer than {AddressMaxLength} characters.");

            return (true, string.Empty);
        }
    }
}