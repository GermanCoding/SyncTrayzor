FROM php:8.5-apache@sha256:9044b746ce59b4e556d47451dbe0b6399c4abbd19780424f19e8c84d1dd85364
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80